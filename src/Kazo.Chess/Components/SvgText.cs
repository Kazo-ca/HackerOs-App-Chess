using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Kazo.Chess.Components;

/// <summary>
/// An SVG <c>&lt;text&gt;</c> element. Razor reserves the <c>&lt;text&gt;</c> tag for its own
/// markup blocks and refuses attributes on it, so the board writes its coordinates through this
/// component instead.
/// </summary>
public sealed class SvgText : ComponentBase
{
    /// <summary>Gets or sets the CSS classes of the element.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>Gets or sets the x coordinate, in board units.</summary>
    [Parameter]
    public double X { get; set; }

    /// <summary>Gets or sets the y coordinate (baseline), in board units.</summary>
    [Parameter]
    public double Y { get; set; }

    /// <summary>Gets or sets the text.</summary>
    [Parameter]
    public string? Text { get; set; }

    /// <inheritdoc />
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "text");
        builder.AddAttribute(1, "class", Class);
        builder.AddAttribute(2, "x", X.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AddAttribute(3, "y", Y.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.AddContent(4, Text);
        builder.CloseElement();
    }
}
