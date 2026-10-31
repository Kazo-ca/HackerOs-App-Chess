using System.Text.Json;
using System.Text.Json.Serialization;
using Kazo.Chess.Model;

namespace Kazo.Chess.Rules;

/// <summary>
/// Reads the JSON snapshot written by <c>wwwroot/js/chess-rules.js</c> (function <c>describe</c>)
/// into a <see cref="GameSnapshot"/>. The JSON crosses the interop boundary as a string and is read
/// with a source-generated context, so the trimmed host keeps everything it needs.
/// </summary>
public static class GameSnapshotJson
{
    /// <summary>Reads a snapshot.</summary>
    /// <param name="json">The JSON text written by the rules module.</param>
    /// <returns>The snapshot.</returns>
    /// <exception cref="FormatException">The text is not a snapshot.</exception>
    public static GameSnapshot Parse(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        SnapshotDto dto;
        try
        {
            dto = JsonSerializer.Deserialize(json, SnapshotJsonContext.Default.SnapshotDto)
                ?? throw new FormatException("The rules module returned an empty snapshot.");
        }
        catch (JsonException exception)
        {
            throw new FormatException("The rules module returned a snapshot that is not valid JSON.", exception);
        }

        return new GameSnapshot(
            dto.Fen ?? throw new FormatException("The snapshot has no FEN."),
            ParseColor(dto.Turn),
            dto.InCheck,
            OutcomeOf(dto),
            (dto.Moves ?? []).Select(ToLegalMove).ToArray(),
            (dto.History ?? []).Select(ToPlayedMove).ToArray(),
            dto.StartFen,
            dto.Result is "1-0" or "0-1" or "1/2-1/2" ? dto.Result : "*");
    }

    // chess.js reports each rule on its own; checkmate and stalemate win over the draw rules
    // (a mate on the fiftieth move still ends the game as a mate).
    private static GameOutcome OutcomeOf(SnapshotDto dto) => dto switch
    {
        { Checkmate: true } => GameOutcome.Checkmate,
        { Stalemate: true } => GameOutcome.Stalemate,
        { InsufficientMaterial: true } => GameOutcome.InsufficientMaterial,
        { ThreefoldRepetition: true } => GameOutcome.ThreefoldRepetition,
        { FiftyMoves: true } => GameOutcome.FiftyMoveRule,
        _ => GameOutcome.Ongoing,
    };

    private static LegalMove ToLegalMove(MoveDto move) => new(
        Square.Parse(move.From ?? string.Empty),
        Square.Parse(move.To ?? string.Empty),
        move.San ?? string.Empty,
        ParsePromotion(move.Promotion));

    private static PlayedMove ToPlayedMove(MoveDto move) => new(
        Square.Parse(move.From ?? string.Empty),
        Square.Parse(move.To ?? string.Empty),
        move.San ?? string.Empty,
        ParseColor(move.Color),
        ParsePromotion(move.Promotion));

    private static PieceColor ParseColor(string? color) => color switch
    {
        "w" => PieceColor.White,
        "b" => PieceColor.Black,
        _ => throw new FormatException($"'{color}' is not a side ('w' or 'b')."),
    };

    private static PieceType? ParsePromotion(string? letter) =>
        letter is { Length: 1 } && PieceLetters.TryFromLetter(letter[0], out PieceType type) ? type : null;

    internal sealed class SnapshotDto
    {
        public string? StartFen { get; set; }

        public string? Result { get; set; }

        public string? Fen { get; set; }

        public string? Turn { get; set; }

        public bool InCheck { get; set; }

        public bool Checkmate { get; set; }

        public bool Stalemate { get; set; }

        public bool InsufficientMaterial { get; set; }

        public bool ThreefoldRepetition { get; set; }

        public bool FiftyMoves { get; set; }

        public List<MoveDto>? Moves { get; set; }

        public List<MoveDto>? History { get; set; }
    }

    internal sealed class MoveDto
    {
        public string? From { get; set; }

        public string? To { get; set; }

        public string? San { get; set; }

        public string? Promotion { get; set; }

        public string? Color { get; set; }
    }
}

/// <summary>Source-generated JSON metadata of the rules module's snapshot.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GameSnapshotJson.SnapshotDto))]
internal sealed partial class SnapshotJsonContext : JsonSerializerContext;
