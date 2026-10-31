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
    public override string Category => "games";

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
