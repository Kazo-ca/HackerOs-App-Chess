using System.Text.Json;
using Kazo.Chess.Model;
using Kazo.Chess.Pgn;
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
    public Task<GameSnapshot> NewGameAsync(string? fen = null, CancellationToken cancellationToken = default) =>
        ReplaceGameAsync("createGame", fen, cancellationToken);

    /// <inheritdoc />
    public async Task<GameSnapshot> LoadPgnAsync(string pgn, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pgn);

        // The module first: a failure to load it is not the file's fault.
        _module ??= await _javaScript.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
        try
        {
            return await ReplaceGameAsync("loadGame", pgn, cancellationToken);
        }
        catch (JSException exception)
        {
            // The rules module names what it could not read ("Invalid move in PGN: e5").
            throw new FormatException(exception.Message, exception);
        }
    }

    /// <inheritdoc />
    public async Task<string> ExportPgnAsync(PgnTags tags, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tags);
        IJSObjectReference game = _game ?? throw new InvalidOperationException("Start a game with NewGameAsync first.");
        string json = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["Event"] = tags.Event,
            ["Site"] = tags.Site,
            ["Date"] = tags.Date,
            ["White"] = tags.White,
            ["Black"] = tags.Black,
            ["Result"] = tags.Result,
        }, PgnJsonContext.Default.DictionaryStringString);
        return await game.InvokeAsync<string>("pgn", cancellationToken, json);
    }

    // The new game replaces the current one only once it exists and its first snapshot reads: a
    // failure keeps the rules on the game the window still shows.
    private async Task<GameSnapshot> ReplaceGameAsync(string factory, string? argument, CancellationToken cancellationToken)
    {
        _module ??= await _javaScript.InvokeAsync<IJSObjectReference>("import", cancellationToken, ModulePath);
        IJSObjectReference game = await _module.InvokeAsync<IJSObjectReference>(factory, cancellationToken, argument);
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

/// <summary>Source-generated JSON metadata of the PGN tags sent to the rules module.</summary>
[System.Text.Json.Serialization.JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class PgnJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
