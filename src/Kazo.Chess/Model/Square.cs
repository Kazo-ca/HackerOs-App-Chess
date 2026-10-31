namespace Kazo.Chess.Model;

/// <summary>
/// One square of the board, by file (0 = a … 7 = h) and rank (0 = rank 1 … 7 = rank 8).
/// The algebraic name (<c>e4</c>) is the form chess.js and the UCI protocol use, so it is the
/// form that crosses every boundary of the app.
/// </summary>
public readonly record struct Square
{
    /// <summary>Number of files and ranks of the board.</summary>
    public const int Size = 8;

    /// <summary>Creates a square from its file and rank indexes.</summary>
    /// <param name="file">File index, 0 (a) to 7 (h).</param>
    /// <param name="rank">Rank index, 0 (rank 1) to 7 (rank 8).</param>
    /// <exception cref="ArgumentOutOfRangeException">An index is outside 0 to 7.</exception>
    public Square(int file, int rank)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(file);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(file, Size);
        ArgumentOutOfRangeException.ThrowIfNegative(rank);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(rank, Size);
        File = file;
        Rank = rank;
    }

    /// <summary>Gets the file index, 0 (a) to 7 (h).</summary>
    public int File { get; }

    /// <summary>Gets the rank index, 0 (rank 1) to 7 (rank 8).</summary>
    public int Rank { get; }

    /// <summary>Gets the algebraic name of the square, for example <c>e4</c>.</summary>
    public string Name => string.Create(2, this, static (span, square) =>
    {
        span[0] = (char)('a' + square.File);
        span[1] = (char)('1' + square.Rank);
    });

    /// <summary>Gets whether the square is a dark one (a1 is dark).</summary>
    public bool IsDark => (File + Rank) % 2 == 0;

    /// <summary>Gets the 64 squares, a1 first, then b1 … h8.</summary>
    public static IReadOnlyList<Square> All { get; } =
        Enumerable.Range(0, Size * Size).Select(index => new Square(index % Size, index / Size)).ToArray();

    /// <summary>Reads an algebraic square name.</summary>
    /// <param name="name">A name such as <c>e4</c>.</param>
    /// <param name="square">The square, when the name is valid.</param>
    /// <returns><see langword="true"/> when <paramref name="name"/> names a square.</returns>
    public static bool TryParse(string? name, out Square square)
    {
        square = default;
        if (name is not { Length: 2 })
        {
            return false;
        }

        int file = name[0] - 'a';
        int rank = name[1] - '1';
        if (file is < 0 or >= Size || rank is < 0 or >= Size)
        {
            return false;
        }

        square = new Square(file, rank);
        return true;
    }

    /// <summary>Reads an algebraic square name that must be valid.</summary>
    /// <param name="name">A name such as <c>e4</c>.</param>
    /// <returns>The square.</returns>
    /// <exception cref="FormatException"><paramref name="name"/> is not a square name.</exception>
    public static Square Parse(string name) =>
        TryParse(name, out Square square) ? square : throw new FormatException($"'{name}' is not a square name.");

    /// <summary>Returns the square moved by a number of files and ranks, or <see langword="null"/> off the board.</summary>
    /// <param name="files">Files to move (positive towards h).</param>
    /// <param name="ranks">Ranks to move (positive towards rank 8).</param>
    public Square? Offset(int files, int ranks)
    {
        int file = File + files;
        int rank = Rank + ranks;
        return file is >= 0 and < Size && rank is >= 0 and < Size ? new Square(file, rank) : null;
    }

    /// <inheritdoc />
    public override string ToString() => Name;
}
