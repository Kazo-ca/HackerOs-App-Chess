using Kazo.Chess.Model;

namespace Kazo.Chess.Tests.Model;

public sealed class SquareTests
{
    [Theory]
    [InlineData("a1", 0, 0)]
    [InlineData("e4", 4, 3)]
    [InlineData("h8", 7, 7)]
    public void Parse_ReadsFileAndRank(string name, int file, int rank)
    {
        Square square = Square.Parse(name);

        Assert.Equal(file, square.File);
        Assert.Equal(rank, square.Rank);
        Assert.Equal(name, square.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("e")]
    [InlineData("i1")]
    [InlineData("a9")]
    [InlineData("a0")]
    [InlineData("e44")]
    public void TryParse_RefusesWhatIsNotASquare(string? name)
    {
        Assert.False(Square.TryParse(name, out _));
        Assert.Throws<FormatException>(() => Square.Parse(name ?? string.Empty));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(8, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 8)]
    public void Constructor_RefusesIndexesOffTheBoard(int file, int rank) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new Square(file, rank));

    [Fact]
    public void IsDark_FollowsTheBoardPattern()
    {
        Assert.True(Square.Parse("a1").IsDark);
        Assert.False(Square.Parse("h1").IsDark);
        Assert.True(Square.Parse("h8").IsDark);
        Assert.False(Square.Parse("e4").IsDark);
    }

    [Fact]
    public void All_ListsTheSixtyFourSquaresFromA1()
    {
        Assert.Equal(64, Square.All.Count);
        Assert.Equal(64, Square.All.Distinct().Count());
        Assert.Equal("a1", Square.All[0].Name);
        Assert.Equal("h8", Square.All[^1].Name);
    }

    [Fact]
    public void Offset_StopsAtTheEdge()
    {
        Square e4 = Square.Parse("e4");

        Assert.Equal(Square.Parse("f6"), e4.Offset(1, 2));
        Assert.Null(Square.Parse("h1").Offset(1, 0));
        Assert.Null(Square.Parse("a1").Offset(0, -1));
    }
}
