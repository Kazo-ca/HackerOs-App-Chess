using HackerOs.App.Abstractions;

namespace Kazo.Chess;

/// <summary>
/// The app's manifest. The build generates <c>manifest.json</c> from this class (HackerOs.App.Build);
/// it is never instantiated at run time.
/// </summary>
[AppManifest]
public sealed class ChessApp : AppDefinition
{
    /// <inheritdoc />
    public override string Id => "ca.kazo.chess";

    /// <inheritdoc />
    public override string Name => "Chess";

    /// <inheritdoc />
    public override AppKind Kind => AppKind.Window;

    /// <inheritdoc />
    public override Type EntryPoint => typeof(ChessWindow);

    /// <inheritdoc />
    public override Type? MobileEntryPoint => typeof(ChessWindow);

    /// <inheritdoc />
    public override string PublisherId => "pub.kazo";

    /// <inheritdoc />
    public override string Description => "Chess for two players on the same screen or against Stockfish";

    /// <inheritdoc />
    public override string Category => "entertainment";

    /// <inheritdoc />
    /// <remarks>The tag that publishes to the Hack Store must be v + this version.</remarks>
    public override string Version => "1.0.0";

    /// <inheritdoc />
    public override IReadOnlyList<string> Capabilities =>
    [
        AppCapabilities.FileSystemUserHomeRead,
        AppCapabilities.FileSystemUserHomeWrite,
        AppCapabilities.DialogFileOpen,
        AppCapabilities.DialogFileSave,
    ];

    /// <inheritdoc />
    /// <remarks>Chess opens saved games: "Open with" and a double-click on a .pgn file start it.</remarks>
    public override IReadOnlyList<FileHandler> FileHandlers =>
        [new("application/x-chess-pgn", [".pgn"], ["open"])];

    /// <inheritdoc />
    public override IReadOnlyList<string> Icons => ["icons/app.svg"];

    /// <inheritdoc />
    public override IReadOnlyList<AssetManifest> Assets =>
        [new("icons/app.svg", AssetKind.Image, new string('0', 64), "app-icon")];

    /// <inheritdoc />
    public override WindowOptions? Window => new()
    {
        DefaultWidth = 860,
        DefaultHeight = 640,
        MinWidth = 360,
        MinHeight = 420,
    };
}
