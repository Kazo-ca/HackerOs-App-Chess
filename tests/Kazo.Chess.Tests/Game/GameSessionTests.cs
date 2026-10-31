using Kazo.Chess.Clock;
using Kazo.Chess.Engine;
using Kazo.Chess.Game;
using Kazo.Chess.Model;
using Kazo.Chess.Pgn;
using Kazo.Chess.Rules;

namespace Kazo.Chess.Tests.Game;

public sealed class GameSessionTests
{
    private static MoveRequest Move(string uci) =>
        MoveRequest.TryParseUci(uci, out MoveRequest? move) ? move! : throw new ArgumentException(uci);

    private static GameSetup Computer(PieceColor player) =>
        new(GameMode.AgainstComputer, EngineLevel.Easy, player);

    [Fact]
    public async Task StartAsync_TwoPlayers_NeverStartsTheEngine()
    {
        var rules = new FakeRules();
        int engines = 0;
        await using var session = new GameSession(rules, () => { engines++; return new FakeEngine(); });

        await session.StartAsync(GameSetup.TwoPlayers);

        Assert.Equal(0, engines);
        Assert.Equal(PieceColor.White, session.Mover);
        Assert.Equal(PieceColor.White, session.Snapshot!.Turn);
    }

    [Fact]
    public async Task PlayAsync_AgainstTheComputer_PlaysItsReply()
    {
        var rules = new FakeRules();
        var engine = new FakeEngine { Replies = { Move("e7e5") } };
        await using var session = new GameSession(rules, () => engine);
        await session.StartAsync(Computer(PieceColor.White));

        bool played = await session.PlayAsync(Move("e2e4"));

        Assert.True(played);
        Assert.Equal(["e2e4", "e7e5"], rules.Played);
        Assert.Equal(1, engine.NewGames);
        Assert.Equal(EngineLevel.Easy, engine.Levels.Single());
        Assert.Equal(PieceColor.White, session.Mover);
        Assert.False(session.IsThinking);
    }

    [Fact]
    public async Task StartAsync_AsBlack_TheComputerOpens()
    {
        var rules = new FakeRules();
        await using var session = new GameSession(rules, () => new FakeEngine { Replies = { Move("d2d4") } });

        await session.StartAsync(Computer(PieceColor.Black));

        Assert.Equal(["d2d4"], rules.Played);
        Assert.Equal(PieceColor.Black, session.Mover);
    }

    [Fact]
    public async Task PlayAsync_OnTheComputersTurn_IsRefused()
    {
        var rules = new FakeRules();
        var hold = new TaskCompletionSource<MoveRequest?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new FakeEngine { Hold = hold };
        await using var session = new GameSession(rules, () => engine);
        Task start = session.StartAsync(Computer(PieceColor.Black));
        await engine.Searching.Task;

        Assert.True(session.IsThinking);
        Assert.Null(session.Mover);
        Assert.False(await session.PlayAsync(Move("e7e5")));

        hold.SetResult(Move("e2e4"));
        await start;
        Assert.Equal(["e2e4"], rules.Played);
    }

    [Fact]
    public async Task StartAsync_WhileTheComputerThinks_CancelsItsSearchAndDropsItsMove()
    {
        var rules = new FakeRules();
        var engine = new FakeEngine { Hold = new TaskCompletionSource<MoveRequest?>() };
        await using var session = new GameSession(rules, () => engine);
        Task first = session.StartAsync(Computer(PieceColor.Black));
        await engine.Searching.Task;

        await session.StartAsync(GameSetup.TwoPlayers);
        await first;

        Assert.True(engine.Cancelled);
        Assert.Empty(rules.Played);
        Assert.Equal(2, rules.NewGames);
        Assert.Equal(GameMode.TwoPlayers, session.Setup.Mode);
        Assert.Equal(PieceColor.White, session.Mover);
    }

    [Fact]
    public async Task PlayAsync_AnIllegalMove_KeepsThePosition()
    {
        var rules = new FakeRules { Refuse = true };
        await using var session = new GameSession(rules, () => new FakeEngine());
        await session.StartAsync(GameSetup.TwoPlayers);
        GameSnapshot before = session.Snapshot!;

        Assert.False(await session.PlayAsync(Move("e2e5")));
        Assert.Same(before, session.Snapshot);
        Assert.False(session.IsBusy);
    }

    [Fact]
    public async Task AnEngineThatFailsToStart_IsReplacedOnce_AndTheGameStarts()
    {
        var rules = new FakeRules();
        var broken = new FakeEngine { FailOnNewGame = true };
        var working = new FakeEngine();
        var engines = new Queue<FakeEngine>([broken, working]);
        await using var session = new GameSession(rules, engines.Dequeue);

        await session.StartAsync(Computer(PieceColor.White));

        Assert.True(broken.Disposed);
        Assert.Equal(1, working.NewGames);
        Assert.Equal(GameMode.AgainstComputer, session.Setup.Mode);
    }

    [Fact]
    public async Task AStartThatFails_KeepsTheCurrentGameAsItWas()
    {
        var rules = new FakeRules();
        var engines = new Queue<FakeEngine>([new() { FailOnNewGame = true }, new() { FailOnNewGame = true }]);
        await using var session = new GameSession(rules, engines.Dequeue);
        await session.StartAsync(GameSetup.TwoPlayers);
        await session.PlayAsync(Move("e2e4"));
        GameSnapshot before = session.Snapshot!;

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(Computer(PieceColor.White)));

        Assert.Same(before, session.Snapshot);
        Assert.Equal(GameMode.TwoPlayers, session.Setup.Mode);
        Assert.Equal(PieceColor.Black, session.Mover);
        Assert.False(session.IsBusy);
    }

    [Fact]
    public async Task AStartThatFails_LetsTheComputerTakeItsTurnAgain()
    {
        var rules = new FakeRules();
        var engine = new FakeEngine { Hold = new TaskCompletionSource<MoveRequest?>(), Replies = { Move("d2d4") } };
        await using var session = new GameSession(rules, () => engine);
        Task first = session.StartAsync(Computer(PieceColor.Black));
        await engine.Searching.Task;
        rules.FailNewGame = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(GameSetup.TwoPlayers));
        await first;

        Assert.Equal(["d2d4"], rules.Played);
        Assert.Equal(PieceColor.Black, session.Mover);
    }

    [Fact]
    public async Task TwoNewGamesBehindASearch_OnlyTheLastOneIsPlayed()
    {
        var rules = new FakeRules();
        var engine = new FakeEngine { Hold = new TaskCompletionSource<MoveRequest?>(), Replies = { Move("g1f3") } };
        await using var session = new GameSession(rules, () => engine);
        Task thinking = session.StartAsync(Computer(PieceColor.Black));
        await engine.Searching.Task;

        Task a = session.StartAsync(Computer(PieceColor.Black));
        Task b = session.StartAsync(Computer(PieceColor.White));
        await Task.WhenAll(thinking, a, b);

        Assert.Equal(PieceColor.White, session.Setup.PlayerColor);
        Assert.Empty(rules.Played);
        Assert.Equal(PieceColor.White, session.Mover);
        Assert.Equal(1, engine.Searches);
    }

    [Fact]
    public async Task AMoveAskedWhileANewGameStarts_IsRefused()
    {
        var rules = new FakeRules();
        await using var session = new GameSession(rules, () => new FakeEngine());
        await session.StartAsync(GameSetup.TwoPlayers);
        var loading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rules.NewGameGate = loading.Task;

        Task start = session.StartAsync(GameSetup.TwoPlayers);
        bool played = await session.PlayAsync(Move("e2e4"));
        loading.SetResult();
        await start;

        Assert.False(played);
        Assert.Empty(rules.Played);
        Assert.Equal(PieceColor.White, session.Mover);
    }

    [Fact]
    public async Task AMoveFoundAsANewGameStarts_IsNotPlayedInIt()
    {
        var rules = new FakeRules();
        var hold = new TaskCompletionSource<MoveRequest?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var engine = new FakeEngine { Hold = hold, IgnoreStop = true };
        await using var session = new GameSession(rules, () => engine);
        Task thinking = session.StartAsync(Computer(PieceColor.Black));
        await engine.Searching.Task;

        // The new game takes its number before its first await: whichever of the stop and the
        // engine's answer lands first, the move found is the old game's.
        Task restart = session.StartAsync(GameSetup.TwoPlayers);
        hold.SetResult(Move("e2e4"));
        await Task.WhenAll(thinking, restart);

        Assert.Empty(rules.Played);
        Assert.Equal(GameMode.TwoPlayers, session.Setup.Mode);
    }

    [Fact]
    public async Task ASubscriberThatThrows_NeverLocksTheGame()
    {
        var rules = new FakeRules();
        await using var session = new GameSession(rules, () => new FakeEngine());
        bool fail = true;
        session.Changed += () =>
        {
            if (fail)
            {
                fail = false;
                throw new InvalidOperationException("render failed");
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(GameSetup.TwoPlayers));
        await session.StartAsync(GameSetup.TwoPlayers);

        Assert.Equal(PieceColor.White, session.Mover);
    }

    [Fact]
    public async Task AnEngineThatDiesMidGame_IsReported_AndReplacedByTheNextGame()
    {
        var rules = new FakeRules();
        var hold = new TaskCompletionSource<MoveRequest?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var dying = new FakeEngine { Hold = hold };
        var fresh = new FakeEngine();
        var engines = new Queue<FakeEngine>([dying, fresh]);
        await using var session = new GameSession(rules, engines.Dequeue);
        Task start = session.StartAsync(Computer(PieceColor.Black));
        await dying.Searching.Task;

        hold.SetException(new Microsoft.JSInterop.JSException("The chess engine stopped."));
        await Assert.ThrowsAsync<Microsoft.JSInterop.JSException>(() => start);
        await session.StartAsync(Computer(PieceColor.White));

        Assert.True(dying.Disposed);
        Assert.Equal(1, fresh.NewGames);
    }

    [Fact]
    public async Task ARefusedComputerMove_IsReported()
    {
        var rules = new FakeRules();
        await using var session = new GameSession(rules, () => new FakeEngine { Replies = { Move("a1a8") } });
        await session.StartAsync(Computer(PieceColor.White));
        rules.RefusedMove = "a1a8";

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.PlayAsync(Move("e2e4")));
        Assert.False(session.IsThinking);
    }

    [Fact]
    public async Task Changed_IsRaisedAroundEachStep()
    {
        await using var session = new GameSession(new FakeRules(), () => new FakeEngine());
        int changes = 0;
        session.Changed += () => changes++;

        await session.StartAsync(GameSetup.TwoPlayers);

        Assert.True(changes >= 2);
    }

    [Fact]
    public async Task DisposeAsync_ReleasesTheEngineAndTheRules()
    {
        var rules = new FakeRules();
        var engine = new FakeEngine();
        var session = new GameSession(rules, () => engine);
        await session.StartAsync(Computer(PieceColor.White));

        await session.DisposeAsync();

        Assert.True(engine.Disposed);
        Assert.True(rules.Disposed);
    }

    [Fact]
    public async Task TheEngineSeesTheWholeGame_NotOnlyTheLastPosition()
    {
        var rules = new FakeRules();
        var engine = new FakeEngine { Replies = { Move("e7e5"), Move("b8c6") } };
        await using var session = new GameSession(rules, () => engine);
        await session.StartAsync(Computer(PieceColor.White));

        await session.PlayAsync(Move("e2e4"));
        await session.PlayAsync(Move("g1f3"));

        Assert.Equal(["e2e4"], engine.Positions[0].Moves);
        Assert.Equal(["e2e4", "e7e5", "g1f3"], engine.Positions[1].Moves);
        Assert.Equal(Fixture.Snapshot("start.json").Fen, engine.Positions[1].StartFen);
    }

    [Fact]
    public async Task Undo_BetweenTwoPlayers_TakesOneMoveBack()
    {
        var rules = new FakeRules();
        await using var session = new GameSession(rules, () => new FakeEngine());
        await session.StartAsync(GameSetup.TwoPlayers);
        Assert.False(session.CanUndo);
        await session.PlayAsync(Move("e2e4"));
        await session.PlayAsync(Move("e7e5"));

        Assert.True(await session.UndoAsync());

        Assert.Equal([1], rules.Undone);
        Assert.Equal(["e2e4"], rules.Played);
        Assert.Equal(PieceColor.Black, session.Mover);
    }

    [Fact]
    public async Task Undo_AgainstTheComputer_TakesItsReplyBackToo()
    {
        var rules = new FakeRules();
        await using var session = new GameSession(rules, () => new FakeEngine { Replies = { Move("e7e5") } });
        await session.StartAsync(Computer(PieceColor.White));
        await session.PlayAsync(Move("e2e4"));

        Assert.True(await session.UndoAsync());

        Assert.Equal([2], rules.Undone);
        Assert.Empty(rules.Played);
        Assert.Equal(PieceColor.White, session.Mover);
    }

    [Fact]
    public async Task Undo_NeedsAMoveOfThePlayer()
    {
        var rules = new FakeRules();
        await using var session = new GameSession(rules, () => new FakeEngine { Replies = { Move("d2d4") } });

        await session.StartAsync(Computer(PieceColor.Black));

        Assert.Equal(["d2d4"], rules.Played);
        Assert.False(session.CanUndo);
        Assert.False(await session.UndoAsync());
    }

    [Fact]
    public async Task Undo_IsOffWithAClock()
    {
        await using var session = new GameSession(new FakeRules(), () => new FakeEngine(), new ManualTime());
        await session.StartAsync(GameSetup.TwoPlayers with { Clock = TimeControl.Of(5, 0) });
        await session.PlayAsync(Move("e2e4"));

        Assert.False(session.CanUndo);
    }

    [Fact]
    public async Task TheClock_PassesFromSideToSideAsTheyMove()
    {
        var time = new ManualTime();
        await using var session = new GameSession(new FakeRules(), () => new FakeEngine(), time);
        await session.StartAsync(GameSetup.TwoPlayers with { Clock = TimeControl.Of(5, 3) });
        Assert.Null(session.Clock!.Running);

        await session.PlayAsync(Move("e2e4"));
        time.Advance(TimeSpan.FromSeconds(20));
        await session.PlayAsync(Move("e7e5"));

        Assert.Equal(PieceColor.White, session.Clock.Running);
        Assert.Equal(TimeSpan.FromSeconds(283), session.Clock.Remaining(PieceColor.Black));
    }

    [Fact]
    public async Task AFallenFlag_EndsTheGameOnTime()
    {
        var time = new ManualTime();
        var rules = new FakeRules();
        await using var session = new GameSession(rules, () => new FakeEngine(), time);
        await session.StartAsync(GameSetup.TwoPlayers with { Clock = TimeControl.Of(1, 0) });
        await session.PlayAsync(Move("e2e4"));
        time.Advance(TimeSpan.FromSeconds(61));

        Assert.True(session.CheckTime());

        Assert.Equal(PieceColor.Black, session.TimedOut);
        Assert.True(session.IsOver);
        Assert.Null(session.Mover);
        Assert.True(session.Clock!.IsStopped);
        Assert.False(await session.PlayAsync(Move("e7e5")));
        Assert.Equal(["e2e4"], rules.Played);
    }

    [Fact]
    public async Task TheComputersFlag_FallingWhileItThinks_StopsItsSearch()
    {
        var time = new ManualTime();
        var rules = new FakeRules();
        var engine = new FakeEngine { Hold = new TaskCompletionSource<MoveRequest?>() };
        await using var session = new GameSession(rules, () => engine, time);
        await session.StartAsync(Computer(PieceColor.White) with { Clock = TimeControl.Of(1, 0) });

        Task reply = session.PlayAsync(Move("e2e4"));
        await engine.Searching.Task;
        time.Advance(TimeSpan.FromSeconds(61));
        session.CheckTime();
        await reply;

        Assert.Equal(PieceColor.Black, session.TimedOut);
        Assert.True(engine.Cancelled);
        Assert.Equal(["e2e4"], rules.Played);
    }

    [Fact]
    public async Task AFlagFallingWhileANewGameStarts_LeavesTheNewGameAlone()
    {
        var time = new ManualTime();
        var rules = new FakeRules();
        var engine = new FakeEngine { Replies = { Move("d2d4") } };
        await using var session = new GameSession(rules, () => engine, time);
        await session.StartAsync(GameSetup.TwoPlayers with { Clock = TimeControl.Of(1, 0) });
        await session.PlayAsync(Move("e2e4"));
        var loading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rules.NewGameGate = loading.Task;

        Task start = session.StartAsync(Computer(PieceColor.Black));
        time.Advance(TimeSpan.FromSeconds(61));
        Assert.False(session.CheckTime());
        loading.SetResult();
        await start;

        Assert.Null(session.TimedOut);
        Assert.Equal(1, engine.Searches);
        Assert.Equal(["d2d4"], rules.Played);
        Assert.Equal(PieceColor.Black, session.Mover);
    }

    [Fact]
    public async Task AFlagFallingWhileTheMoveIsPlayed_LosesOnTime_WithoutIncrement()
    {
        var time = new ManualTime();
        var rules = new FakeRules();
        await using var session = new GameSession(rules, () => new FakeEngine(), time);
        await session.StartAsync(GameSetup.TwoPlayers with { Clock = TimeControl.Of(1, 5) });
        await session.PlayAsync(Move("e2e4"));
        var playing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        rules.MoveGate = playing.Task;

        Task<bool> move = session.PlayAsync(Move("e7e5"));
        time.Advance(TimeSpan.FromSeconds(61));
        playing.SetResult();
        await move;

        Assert.Equal(PieceColor.Black, session.TimedOut);
        Assert.Equal(TimeSpan.Zero, session.Clock!.Remaining(PieceColor.Black));
        Assert.True(session.Clock.IsStopped);
    }

    [Fact]
    public async Task LoadAsync_OpensASavedGameBetweenTwoPlayersWithoutClock()
    {
        var rules = new FakeRules();
        await using var session = new GameSession(rules, () => new FakeEngine(), new ManualTime());
        await session.StartAsync(Computer(PieceColor.White) with { Clock = TimeControl.Of(5, 0) });

        await session.LoadAsync("e2e4 e7e5 g1f3");

        Assert.Equal(GameMode.TwoPlayers, session.Setup.Mode);
        Assert.Null(session.Clock);
        Assert.Equal(3, session.Snapshot!.History.Count);
        Assert.Equal(PieceColor.Black, session.Mover);
        Assert.Null(session.RecordedResult);
    }

    [Fact]
    public async Task LoadAsync_AnUnreadableGame_KeepsTheCurrentOne()
    {
        var rules = new FakeRules();
        await using var session = new GameSession(rules, () => new FakeEngine());
        await session.StartAsync(GameSetup.TwoPlayers);
        await session.PlayAsync(Move("e2e4"));
        GameSnapshot before = session.Snapshot!;

        await Assert.ThrowsAsync<FormatException>(() => session.LoadAsync("bad"));

        Assert.Same(before, session.Snapshot);
        Assert.Equal(PieceColor.Black, session.Mover);
        Assert.False(session.IsBusy);
    }

    [Fact]
    public async Task AGameSavedAsResigned_IsOverUntilAMoveIsTakenBack()
    {
        var rules = new FakeRules { Recorded = "1-0" };
        await using var session = new GameSession(rules, () => new FakeEngine());
        await session.StartAsync(GameSetup.TwoPlayers);

        await session.LoadAsync("e2e4 e7e5");

        Assert.Equal("1-0", session.RecordedResult);
        Assert.True(session.IsOver);
        Assert.Null(session.Mover);
        Assert.Equal(new GameStatus("StatusRecorded", null, "1-0"), GameStatus.Of(session.Snapshot!, null, session.RecordedResult));

        Assert.True(await session.UndoAsync());
        Assert.Null(session.RecordedResult);
        Assert.Equal(PieceColor.Black, session.Mover);
    }

    [Fact]
    public async Task ExportPgnAsync_NamesThePlayersAndTheResult()
    {
        var rules = new FakeRules();
        await using var session = new GameSession(rules, () => new FakeEngine { Replies = { Move("e7e5") } });
        await session.StartAsync(Computer(PieceColor.White) with { Level = EngineLevel.Hard });
        await session.PlayAsync(Move("e2e4"));

        string pgn = await session.ExportPgnAsync(new DateOnly(2026, 10, 31), "neo");

        Assert.Equal("[Result \"*\"]", pgn);
        Assert.Equal(new PgnTags("Casual game", "HackerOS", "2026.10.31", "neo", "Stockfish 19 lite (Skill 15)", "*"), rules.Exported);
    }

    [Fact]
    public async Task ExportPgnAsync_BetweenTwoPlayers_LeavesTheNamesUnknown()
    {
        var rules = new FakeRules();
        await using var session = new GameSession(rules, () => new FakeEngine());
        await session.StartAsync(GameSetup.TwoPlayers);

        await session.ExportPgnAsync(new DateOnly(2026, 10, 31), "neo");

        Assert.Equal("?", rules.Exported!.White);
        Assert.Equal("?", rules.Exported.Black);
    }

    [Fact]
    public async Task ANewGame_ComesWithAFreshClock()
    {
        var time = new ManualTime();
        await using var session = new GameSession(new FakeRules(), () => new FakeEngine(), time);
        await session.StartAsync(GameSetup.TwoPlayers with { Clock = TimeControl.Of(1, 0) });
        await session.PlayAsync(Move("e2e4"));
        time.Advance(TimeSpan.FromSeconds(61));
        session.CheckTime();

        await session.StartAsync(GameSetup.TwoPlayers);

        Assert.Null(session.Clock);
        Assert.Null(session.TimedOut);
        Assert.Equal(PieceColor.White, session.Mover);
    }

    // Rules that alternate the side to move and record the moves; the board itself does not
    // matter, the history does (undo and the engine's position read it).
    private sealed class FakeRules : IChessRules
    {
        private static readonly GameSnapshot Start = Fixture.Snapshot("start.json");
        private GameSnapshot _current = Start;

        private GameSnapshot White => With(PieceColor.White);

        private GameSnapshot Black => With(PieceColor.Black);

        public List<string> Played { get; } = [];

        public int NewGames { get; private set; }

        public bool Refuse { get; set; }

        public string? RefusedMove { get; set; }

        public bool FailNewGame { get; set; }

        public Task? NewGameGate { get; set; }

        public bool Disposed { get; private set; }

        public async Task<GameSnapshot> NewGameAsync(string? fen = null, CancellationToken cancellationToken = default)
        {
            if (NewGameGate is { } gate)
            {
                await gate;
            }

            if (FailNewGame)
            {
                throw new InvalidOperationException("The rules could not load.");
            }

            NewGames++;
            Played.Clear();
            _current = White;
            return _current;
        }

        // A snapshot whose history is the moves played, the side to move given.
        private GameSnapshot With(PieceColor turn)
        {
            PlayedMove[] history = Played.Select((uci, index) =>
            {
                MoveRequest.TryParseUci(uci, out MoveRequest? move);
                return new PlayedMove(move!.From, move.To, uci, index % 2 == 0 ? PieceColor.White : PieceColor.Black, move.Promotion);
            }).ToArray();
            return new GameSnapshot(Start.Fen, turn, false, GameOutcome.Ongoing, Start.LegalMoves, history);
        }

        public Task? MoveGate { get; set; }

        public async Task<GameSnapshot?> TryMoveAsync(MoveRequest move, CancellationToken cancellationToken = default)
        {
            if (MoveGate is { } gate)
            {
                await gate;
            }

            if (Refuse || move.Uci == RefusedMove)
            {
                return null;
            }

            PieceColor next = _current.Turn == PieceColor.White ? PieceColor.Black : PieceColor.White;
            Played.Add(move.Uci);
            _current = With(next);
            return _current;
        }

        public List<int> Undone { get; } = [];

        public string? Recorded { get; set; }

        public PgnTags? Exported { get; private set; }

        public Task<GameSnapshot> LoadPgnAsync(string pgn, CancellationToken cancellationToken = default)
        {
            if (pgn == "bad")
            {
                throw new FormatException("Invalid move in PGN: e5");
            }

            Played.Clear();
            Played.AddRange(pgn.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            GameSnapshot played = With(Played.Count % 2 == 0 ? PieceColor.White : PieceColor.Black);
            _current = new GameSnapshot(played.Fen, played.Turn, false, GameOutcome.Ongoing, played.LegalMoves, played.History, null, Recorded ?? "*");
            return Task.FromResult(_current);
        }

        public Task<string> ExportPgnAsync(PgnTags tags, CancellationToken cancellationToken = default)
        {
            Exported = tags;
            return Task.FromResult($"[Result \"{tags.Result}\"]");
        }

        public Task<GameSnapshot> UndoAsync(int plies, CancellationToken cancellationToken = default)
        {
            Undone.Add(plies);
            Played.RemoveRange(Math.Max(0, Played.Count - plies), Math.Min(plies, Played.Count));
            _current = With(Played.Count % 2 == 0 ? PieceColor.White : PieceColor.Black);
            return Task.FromResult(_current);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeEngine : IChessEngine
    {
        public List<MoveRequest> Replies { get; } = [];

        public TaskCompletionSource<MoveRequest?>? Hold { get; set; }

        public TaskCompletionSource Searching { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<EngineLevel> Levels { get; } = [];

        public int NewGames { get; private set; }

        public bool FailOnNewGame { get; set; }

        public bool IgnoreStop { get; set; }

        public int Searches { get; private set; }

        public bool Cancelled { get; private set; }

        public bool Disposed { get; private set; }

        public Task NewGameAsync(CancellationToken cancellationToken = default)
        {
            if (FailOnNewGame)
            {
                throw new InvalidOperationException("The worker could not load.");
            }

            NewGames++;
            return Task.CompletedTask;
        }

        public List<EnginePosition> Positions { get; } = [];

        public async Task<MoveRequest?> BestMoveAsync(EnginePosition position, EngineLevel level, CancellationToken cancellationToken = default)
        {
            // Like StockfishEngine: an already cancelled search never starts.
            cancellationToken.ThrowIfCancellationRequested();
            Positions.Add(position);
            Levels.Add(level);
            Searches++;
            Searching.TrySetResult();
            TaskCompletionSource<MoveRequest?>? hold = Hold;
            Hold = null;
            if (hold is null)
            {
                MoveRequest? reply = Replies.Count > 0 ? Replies[0] : null;
                if (reply is not null)
                {
                    Replies.RemoveAt(0);
                }

                return reply;
            }

            // Like Stockfish: a stop request ends the search at once (or, with IgnoreStop, the
            // move it had already found comes back anyway).
            using CancellationTokenRegistration stop = cancellationToken.Register(() =>
            {
                Cancelled = true;
                if (!IgnoreStop)
                {
                    hold.TrySetCanceled(cancellationToken);
                }
            });
            return await hold.Task;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
