using Kazo.Chess.Engine;
using Kazo.Chess.Model;
using Microsoft.JSInterop;

namespace Kazo.Chess.Tests.Engine;

public sealed class StockfishEngineTests
{
    private const string StartFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

    [Fact]
    public async Task TheWorkerStartsOnceForEveryCall()
    {
        var js = new FakeRuntime();
        await using var engine = new StockfishEngine(js);

        await engine.NewGameAsync();
        await engine.BestMoveAsync(StartFen, EngineLevel.Easy);
        await engine.BestMoveAsync(StartFen, EngineLevel.Easy);

        Assert.Equal(1, js.Imports);
        Assert.Equal(StockfishEngine.ModulePath, js.ImportedPath);
        Assert.Equal(1, js.Module.Created);
        Assert.Equal(["newGame", "bestMove", "bestMove"], js.Module.Worker.Calls.Select(call => call.Name));
    }

    [Fact]
    public async Task BestMoveAsync_SendsTheLevelsSkillAndThinkingTime()
    {
        var js = new FakeRuntime();
        await using var engine = new StockfishEngine(js);
        js.Module.Worker.Reply = "bestmove g1f3 ponder d7d5";

        MoveRequest? move = await engine.BestMoveAsync(StartFen, EngineLevel.Hard);

        Assert.Equal("g1f3", move!.Uci);
        object?[] args = js.Module.Worker.Calls.Single().Args;
        Assert.Equal([StartFen, 15, EngineLevel.Hard.MoveTimeMs()], args);
    }

    [Fact]
    public async Task BestMoveAsync_ReturnsNullWhenThePositionHasNoMove()
    {
        var js = new FakeRuntime();
        await using var engine = new StockfishEngine(js);
        js.Module.Worker.Reply = "bestmove (none)";

        Assert.Null(await engine.BestMoveAsync(StartFen, EngineLevel.Beginner));
    }

    [Fact]
    public async Task ACancelledSearch_StopsTheWorkerAndThrows()
    {
        var js = new FakeRuntime();
        await using var engine = new StockfishEngine(js);
        var reply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        js.Module.Worker.PendingReply = reply;
        using var cancel = new CancellationTokenSource();

        Task<MoveRequest?> search = engine.BestMoveAsync(StartFen, EngineLevel.Expert, cancel.Token);
        await js.Module.Worker.Searching.Task;
        await cancel.CancelAsync();
        // Stockfish answers a stop with the best move found so far.
        reply.SetResult("bestmove e2e4");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => search);
        Assert.Contains("stop", js.Module.Worker.Calls.Select(call => call.Name));
    }

    [Fact]
    public async Task AnAlreadyCancelledSearch_NeverReachesTheWorker()
    {
        var js = new FakeRuntime();
        await using var engine = new StockfishEngine(js);
        await engine.NewGameAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.BestMoveAsync(StartFen, EngineLevel.Easy, new CancellationToken(canceled: true)));

        Assert.DoesNotContain("bestMove", js.Module.Worker.Calls.Select(call => call.Name));
    }

    [Fact]
    public async Task AWorkerCreatedAfterTheEngineWasClosed_IsEndedAtOnce()
    {
        var js = new FakeRuntime();
        var engine = new StockfishEngine(js);
        var created = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        js.Module.Gate = created.Task;

        Task start = engine.NewGameAsync();
        await engine.DisposeAsync();
        created.SetResult();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => start);
        Assert.Equal(["terminate"], js.Module.Worker.Calls.Select(call => call.Name));
    }

    [Fact]
    public async Task DisposeAsync_TerminatesTheWorker_EvenWhenThePageIsGone()
    {
        var js = new FakeRuntime();
        var engine = new StockfishEngine(js);
        await engine.NewGameAsync();
        js.Module.Worker.ThrowOnTerminate = true;

        await engine.DisposeAsync();

        Assert.Contains("terminate", js.Module.Worker.Calls.Select(call => call.Name));
    }

    [Fact]
    public async Task DisposeAsync_BeforeAnyCall_DoesNothing()
    {
        var js = new FakeRuntime();

        await new StockfishEngine(js).DisposeAsync();

        Assert.Equal(0, js.Imports);
    }

    [Fact]
    public async Task BestMoveAsync_RefusesAnEmptyPosition()
    {
        await using var engine = new StockfishEngine(new FakeRuntime());

        await Assert.ThrowsAsync<ArgumentException>(() => engine.BestMoveAsync(" ", EngineLevel.Easy));
    }

    private sealed record Call(string Name, object?[] Args);

    private sealed class FakeRuntime : IJSRuntime
    {
        public FakeModule Module { get; } = new();

        public int Imports { get; private set; }

        public string? ImportedPath { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Assert.Equal("import", identifier);
            Imports++;
            ImportedPath = (string?)args?[0];
            return ValueTask.FromResult((TValue)(object)Module);
        }
    }

    private sealed class FakeModule : IJSObjectReference
    {
        public FakeWorker Worker { get; } = new();

        public int Created { get; private set; }

        public Task? Gate { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Assert.Equal("createEngine", identifier);
            Created++;
            if (Gate is not null)
            {
                await Gate;
            }

            return (TValue)(object)Worker;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeWorker : IJSObjectReference
    {
        public List<Call> Calls { get; } = [];

        public string Reply { get; set; } = "bestmove e2e4";

        public TaskCompletionSource<string>? PendingReply { get; set; }

        public TaskCompletionSource Searching { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool ThrowOnTerminate { get; set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add(new Call(identifier, args ?? []));
            switch (identifier)
            {
                case "bestMove":
                    Searching.TrySetResult();
                    string line = PendingReply is null ? Reply : await PendingReply.Task;
                    return (TValue)(object)line;
                case "terminate" when ThrowOnTerminate:
                    throw new JSDisconnectedException("gone");
                default:
                    // newGame, stop, terminate: no result (InvokeVoidAsync reads an IJSVoidResult).
                    return default!;
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
