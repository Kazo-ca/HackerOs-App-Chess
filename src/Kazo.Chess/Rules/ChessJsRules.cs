using Kazo.Chess.Model;
using Microsoft.JSInterop;

namespace Kazo.Chess.Rules;

/// <summary>
/// The rules of chess from chess.js, served by this app at <c>_content/Kazo.Chess/</c> and reached
/// through <c>js/chess-rules.js</c>. Each call returns the whole position as one JSON string, read by
/// <see cref="GameSnapshotJson"/>.
/// </summary>
public sealed class ChessJsRules : IChessRules
{
    /// <summary>Path of the rules module, relative to the host's base address.</summary>
    public const string ModulePath = "./_content/Kazo.Chess/js/chess-rules.js";

    private readonly IJSRuntime _javaScript;
    private IJSObjectReference? _module;
    private IJSObjectReference? _game;

    /// <summary>Creates the rules over a JavaScript runtime.</summary>
    /// <param name="javaScript">The browser's JavaScript runtime.</param>
    public ChessJsRules(IJSRuntime javaScript)
    {
        ArgumentNullException.ThrowIfNull(javaScript);
        _javaScript = javaScript;
    }

    /// <inheritdoc />
    public async Task<GameSnapshot> NewGameAsync(string? fen = null, CancellationToken cancellationToken = default)
    {
        _module ??= await _javaScript.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);

        // The new game replaces the current one only once it exists and its first snapshot reads:
        // a failure keeps the rules on the game the window still shows.
        IJSObjectReference game = await _module.InvokeAsync<IJSObjectReference>("createGame", cancellationToken, fen);
        GameSnapshot snapshot;
        try
        {
            snapshot = GameSnapshotJson.Parse(await game.InvokeAsync<string>("snapshot", cancellationToken));
        }
        catch
        {
            await ReleaseAsync(game);
            throw;
        }

        IJSObjectReference? previous = _game;
        _game = game;
        await ReleaseAsync(previous);
        return snapshot;
    }

    /// <inheritdoc />
    public async Task<GameSnapshot?> TryMoveAsync(MoveRequest move, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(move);
        IJSObjectReference game = _game ?? throw new InvalidOperationException("Start a game with NewGameAsync first.");
        string? promotion = move.Promotion is { } piece ? PieceLetters.ToLetter(piece).ToString() : null;
        string? json = await game.InvokeAsync<string?>("move", cancellationToken, move.From.Name, move.To.Name, promotion);
        return json is null ? null : GameSnapshotJson.Parse(json);
    }

    /// <inheritdoc />
    public async Task<GameSnapshot> UndoAsync(int plies, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(plies);
        IJSObjectReference game = _game ?? throw new InvalidOperationException("Start a game with NewGameAsync first.");
        return GameSnapshotJson.Parse(await game.InvokeAsync<string>("undo", cancellationToken, plies));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        IJSObjectReference? game = _game;
        IJSObjectReference? module = _module;
        _game = null;
        _module = null;
        await ReleaseAsync(game);
        await ReleaseAsync(module);
    }

    private static async ValueTask ReleaseAsync(IJSObjectReference? reference)
    {
        if (reference is null)
        {
            return;
        }

        try
        {
            await reference.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The page is gone: nothing left to release on the JavaScript side.
        }
    }
}
