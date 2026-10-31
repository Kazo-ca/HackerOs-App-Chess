using Kazo.Chess.Model;
using Kazo.Chess.Pgn;
using Kazo.Chess.Rules;
using Microsoft.JSInterop;

namespace Kazo.Chess.Tests.Rules;

public sealed class ChessJsRulesTests
{
    [Fact]
    public async Task NewGameAsync_ImportsTheModuleOnceAndReturnsTheSnapshot()
    {
        var js = new FakeRuntime();
        await using var rules = new ChessJsRules(js);

        GameSnapshot first = await rules.NewGameAsync();
        GameSnapshot second = await rules.NewGameAsync("8/P6k/8/8/8/8/8/K7 w - - 0 1");

        Assert.Equal(1, js.Imports);
        Assert.Equal(ChessJsRules.ModulePath, js.ImportedPath);
        Assert.Equal(20, first.LegalMoves.Count);
        Assert.Equal("8/P6k/8/8/8/8/8/K7 w - - 0 1", js.Module.CreatedWith[^1]);
        Assert.True(js.Module.Games[0].Disposed, "the replaced game is released");
        Assert.False(js.Module.Games[1].Disposed);
        Assert.NotNull(second);
    }

    [Fact]
    public async Task NewGameAsync_ThatFails_KeepsTheCurrentGame()
    {
        var js = new FakeRuntime();
        await using var rules = new ChessJsRules(js);
        await rules.NewGameAsync();
        js.Module.NextSnapshot = "{ not json";

        await Assert.ThrowsAsync<FormatException>(() => rules.NewGameAsync());

        FakeGame current = js.Module.Games[0];
        FakeGame failed = js.Module.Games[1];
        Assert.False(current.Disposed, "the game the window still shows stays");
        Assert.True(failed.Disposed, "the half-made game is released");
        await rules.TryMoveAsync(new MoveRequest(Square.Parse("e2"), Square.Parse("e4")));
        Assert.Equal(["e2e4"], current.Moves);
    }

    [Fact]
    public async Task TryMoveAsync_SendsSquaresAndALowerCasePromotion()
    {
        var js = new FakeRuntime();
        await using var rules = new ChessJsRules(js);
        await rules.NewGameAsync();

        await rules.TryMoveAsync(new MoveRequest(Square.Parse("a7"), Square.Parse("a8"), PieceType.Knight));

        Assert.Equal(["a7a8n"], js.Module.Games[0].Moves);
    }

    [Fact]
    public async Task TryMoveAsync_ReturnsNullForAnIllegalMove()
    {
        var js = new FakeRuntime();
        await using var rules = new ChessJsRules(js);
        await rules.NewGameAsync();
        js.Module.Games[0].Refuse = true;

        Assert.Null(await rules.TryMoveAsync(new MoveRequest(Square.Parse("e2"), Square.Parse("e5"))));
    }

    [Fact]
    public async Task TryMoveAsync_BeforeANewGame_Throws()
    {
        await using var rules = new ChessJsRules(new FakeRuntime());

        await Assert.ThrowsAsync<InvalidOperationException>(() => rules.TryMoveAsync(new MoveRequest(Square.Parse("e2"), Square.Parse("e4"))));
    }

    [Fact]
    public async Task DisposeAsync_ReleasesTheGameAndTheModule_EvenWhenThePageIsGone()
    {
        var js = new FakeRuntime();
        var rules = new ChessJsRules(js);
        await rules.NewGameAsync();
        js.Module.Games[0].ThrowOnDispose = true;

        await rules.DisposeAsync();

        Assert.True(js.Module.Disposed);
    }

    [Fact]
    public async Task LoadPgnAsync_ReplacesTheGame_AndAnUnreadableTextIsAFormatError()
    {
        var js = new FakeRuntime();
        await using var rules = new ChessJsRules(js);
        await rules.NewGameAsync();

        await rules.LoadPgnAsync("1. e4 *");
        js.Module.FailNextLoad = true;
        FormatException error = await Assert.ThrowsAsync<FormatException>(() => rules.LoadPgnAsync("1. e5"));

        Assert.Equal(["1. e4 *", "1. e5"], js.Module.Loaded);
        Assert.Contains("Invalid move in PGN", error.Message, StringComparison.Ordinal);
        Assert.True(js.Module.Games[0].Disposed, "the loaded game replaced the first one");
        Assert.False(js.Module.Games[1].Disposed, "a failed load keeps the current game");
    }

    [Fact]
    public async Task LoadPgnAsync_AModuleThatDoesNotLoad_IsNotCalledABadFile()
    {
        var js = new FakeRuntime { FailImport = true };
        await using var rules = new ChessJsRules(js);

        await Assert.ThrowsAsync<JSException>(() => rules.LoadPgnAsync("1. e4 *"));
    }

    [Fact]
    public async Task ExportPgnAsync_SendsTheTagsAsJson()
    {
        var js = new FakeRuntime();
        await using var rules = new ChessJsRules(js);
        await rules.NewGameAsync();

        string pgn = await rules.ExportPgnAsync(new PgnTags("Casual game", "HackerOS", "2026.10.31", "neo", "?", "1-0"));

        Assert.Equal("[pgn]", pgn);
        Assert.Equal(["pgn {\"Event\":\"Casual game\",\"Site\":\"HackerOS\",\"Date\":\"2026.10.31\",\"White\":\"neo\",\"Black\":\"?\",\"Result\":\"1-0\"}"], js.Module.Games[0].Moves);
    }

    [Fact]
    public async Task UndoAsync_AsksForThePliesAndReadsThePosition()
    {
        var js = new FakeRuntime();
        await using var rules = new ChessJsRules(js);
        await rules.NewGameAsync();

        GameSnapshot snapshot = await rules.UndoAsync(2);

        Assert.Equal(["undo2"], js.Module.Games[0].Moves);
        Assert.Equal(PieceColor.White, snapshot.Turn);
    }

    private sealed class FakeRuntime : IJSRuntime
    {
        public FakeModule Module { get; } = new();

        public int Imports { get; private set; }

        public string? ImportedPath { get; private set; }

        public bool FailImport { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            FailImport ? throw new JSException("Failed to fetch dynamically imported module") : Import<TValue>(identifier, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        private ValueTask<TValue> Import<TValue>(string identifier, object?[]? args)
        {
            Assert.Equal("import", identifier);
            Imports++;
            ImportedPath = (string?)args?[0];
            return ValueTask.FromResult((TValue)(object)Module);
        }
    }

    private abstract class FakeReference : IJSObjectReference
    {
        public bool Disposed { get; private set; }

        public bool ThrowOnDispose { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public abstract ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args);

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ThrowOnDispose ? ValueTask.FromException(new JSDisconnectedException("gone")) : ValueTask.CompletedTask;
        }
    }

    private sealed class FakeModule : FakeReference
    {
        public List<FakeGame> Games { get; } = [];

        public List<string?> CreatedWith { get; } = [];

        public string? NextSnapshot { get; set; }

        public List<string?> Loaded { get; } = [];

        public bool FailNextLoad { get; set; }

        public override ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "loadGame")
            {
                Loaded.Add((string?)args?[0]);
                if (FailNextLoad)
                {
                    FailNextLoad = false;
                    throw new JSException("Invalid move in PGN: e5");
                }
            }
            else
            {
                Assert.Equal("createGame", identifier);
                CreatedWith.Add((string?)args?[0]);
            }

            var game = new FakeGame(NextSnapshot ?? Fixture.Json("start.json"));
            NextSnapshot = null;
            Games.Add(game);
            return ValueTask.FromResult((TValue)(object)game);
        }
    }

    private sealed class FakeGame(string snapshot) : FakeReference
    {
        public List<string> Moves { get; } = [];

        public bool Refuse { get; set; }

        public override ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            switch (identifier)
            {
                case "snapshot":
                    return ValueTask.FromResult((TValue)(object)snapshot);
                case "pgn":
                    Moves.Add($"pgn {args![0]}");
                    return ValueTask.FromResult((TValue)(object)"[pgn]");
                case "undo":
                    Moves.Add($"undo{args![0]}");
                    return ValueTask.FromResult((TValue)(object)snapshot);
                case "move":
                    Moves.Add($"{args![0]}{args[1]}{args[2]}");
                    return ValueTask.FromResult(Refuse ? default! : (TValue)(object)Fixture.Json("after-e4.json"));
                default:
                    throw new InvalidOperationException($"Unexpected call '{identifier}'.");
            }
        }
    }
}
