using Kazo.Chess.Engine;
using Kazo.Chess.Game;
using Kazo.Chess.Model;
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
        var engine = new FakeEngine { Hold = new TaskCompletionSource<MoveRequest?>() };
        await using var session = new GameSession(rules, () => engine);
        Task start = session.StartAsync(Computer(PieceColor.Black));
        await engine.Searching.Task;

        Assert.True(session.IsThinking);
        Assert.Null(session.Mover);
        Assert.False(await session.PlayAsync(Move("e7e5")));

        engine.Hold.SetResult(Move("e2e4"));
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
    public async Task AnEngineThatFailsToStart_IsReplacedByTheNextGame()
    {
        var rules = new FakeRules();
        var broken = new FakeEngine { FailOnNewGame = true };
        var working = new FakeEngine();
        var engines = new Queue<FakeEngine>([broken, working]);
        await using var session = new GameSession(rules, engines.Dequeue);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(Computer(PieceColor.White)));
        await session.StartAsync(Computer(PieceColor.White));

        Assert.True(broken.Disposed);
        Assert.Equal(1, working.NewGames);
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

    // Rules that alternate the side to move and record the moves; the position itself does not matter.
    private sealed class FakeRules : IChessRules
    {
        private static readonly GameSnapshot White = Fixture.Snapshot("start.json");
        private static readonly GameSnapshot Black = Fixture.Snapshot("after-e4.json");
        private GameSnapshot _current = White;

        public List<string> Played { get; } = [];

        public int NewGames { get; private set; }

        public bool Refuse { get; set; }

        public string? RefusedMove { get; set; }

        public bool Disposed { get; private set; }

        public Task<GameSnapshot> NewGameAsync(string? fen = null, CancellationToken cancellationToken = default)
        {
            NewGames++;
            _current = White;
            return Task.FromResult(_current);
        }

        public Task<GameSnapshot?> TryMoveAsync(MoveRequest move, CancellationToken cancellationToken = default)
        {
            if (Refuse || move.Uci == RefusedMove)
            {
                return Task.FromResult<GameSnapshot?>(null);
            }

            Played.Add(move.Uci);
            _current = _current.Turn == PieceColor.White ? Black : White;
            return Task.FromResult<GameSnapshot?>(_current);
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

        public async Task<MoveRequest?> BestMoveAsync(string fen, EngineLevel level, CancellationToken cancellationToken = default)
        {
            Levels.Add(level);
            Searching.TrySetResult();
            if (Hold is null)
            {
                MoveRequest? reply = Replies.Count > 0 ? Replies[0] : null;
                if (reply is not null)
                {
                    Replies.RemoveAt(0);
                }

                return reply;
            }

            // Like Stockfish: a stop request ends the search at once.
            using CancellationTokenRegistration stop = cancellationToken.Register(() =>
            {
                Cancelled = true;
                Hold.TrySetCanceled(cancellationToken);
            });
            return await Hold.Task;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
