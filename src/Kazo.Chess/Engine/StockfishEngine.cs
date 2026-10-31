using Kazo.Chess.Model;
using Microsoft.JSInterop;

namespace Kazo.Chess.Engine;

/// <summary>
/// Stockfish 19 "lite, single-threaded" (stockfish.js, GPL-3.0) in a Web Worker, served by this app
/// at <c>_content/Kazo.Chess/engine/</c> and reached through <c>js/chess-engine.js</c>. The worker
/// starts on the first call, so a game between two players never downloads the engine.
/// </summary>
public sealed class StockfishEngine : IChessEngine
{
    /// <summary>Path of the engine module, relative to the host's base address.</summary>
    public const string ModulePath = "./_content/Kazo.Chess/js/chess-engine.js";

    private readonly IJSRuntime _javaScript;
    private readonly SemaphoreSlim _start = new(1, 1);
    private IJSObjectReference? _module;
    private IJSObjectReference? _engine;

    /// <summary>Creates the engine over a JavaScript runtime.</summary>
    /// <param name="javaScript">The browser's JavaScript runtime.</param>
    public StockfishEngine(IJSRuntime javaScript)
    {
        ArgumentNullException.ThrowIfNull(javaScript);
        _javaScript = javaScript;
    }

    /// <inheritdoc />
    public async Task NewGameAsync(CancellationToken cancellationToken = default)
    {
        IJSObjectReference engine = await StartAsync(cancellationToken);
        await engine.InvokeVoidAsync("newGame", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<MoveRequest?> BestMoveAsync(string fen, EngineLevel level, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fen);
        IJSObjectReference engine = await StartAsync(cancellationToken);

        // A cancelled search is stopped in the worker (its bestmove then comes at once); the token
        // is not passed to the interop call, so the reply is still read and the queue stays in order.
        await using CancellationTokenRegistration stop = cancellationToken.Register(() => _ = StopAsync(engine));
        string line = await engine.InvokeAsync<string>("bestMove", fen, level.SkillLevel(), level.MoveTimeMs());
        cancellationToken.ThrowIfCancellationRequested();
        return ParseBestMove(line);
    }

    /// <summary>Reads the move of a UCI <c>bestmove</c> line (<c>bestmove e2e4 ponder e7e5</c>).</summary>
    /// <param name="line">The line the engine wrote.</param>
    /// <returns>The move, or <see langword="null"/> for <c>bestmove (none)</c>.</returns>
    /// <exception cref="FormatException">The line is not a <c>bestmove</c> line.</exception>
    public static MoveRequest? ParseBestMove(string line)
    {
        string[] words = (line ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words is not ["bestmove", _, ..])
        {
            throw new FormatException($"'{line}' is not a UCI bestmove line.");
        }

        if (words[1] == "(none)")
        {
            return null;
        }

        return MoveRequest.TryParseUci(words[1], out MoveRequest? move)
            ? move
            : throw new FormatException($"'{words[1]}' is not a UCI move.");
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        IJSObjectReference? engine = _engine;
        IJSObjectReference? module = _module;
        _engine = null;
        _module = null;
        try
        {
            if (engine is not null)
            {
                await engine.InvokeVoidAsync("terminate");
                await engine.DisposeAsync();
            }

            if (module is not null)
            {
                await module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The page is gone, and the worker with it.
        }

        _start.Dispose();
    }

    private static async Task StopAsync(IJSObjectReference engine)
    {
        try
        {
            await engine.InvokeVoidAsync("stop");
        }
        catch (Exception exception) when (exception is JSException or JSDisconnectedException or ObjectDisposedException)
        {
            // The engine is already gone: nothing left to stop.
        }
    }

    // One start at a time: two quick calls share the same worker.
    private async Task<IJSObjectReference> StartAsync(CancellationToken cancellationToken)
    {
        if (_engine is { } running)
        {
            return running;
        }

        await _start.WaitAsync(cancellationToken);
        try
        {
            _module ??= await _javaScript.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
            _engine ??= await _module.InvokeAsync<IJSObjectReference>("createEngine", cancellationToken);
            return _engine;
        }
        finally
        {
            _start.Release();
        }
    }
}
