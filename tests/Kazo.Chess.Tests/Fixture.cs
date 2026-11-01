using Kazo.Chess.Model;
using Kazo.Chess.Rules;

namespace Kazo.Chess.Tests;

/// <summary>Reads the chess.js snapshots written by <c>tests/js/write-fixtures.mjs</c>.</summary>
internal static class Fixture
{
    public static string Json(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    public static GameSnapshot Snapshot(string name) => GameSnapshotJson.Parse(Json(name));
}
