using HackerOs.AppSdk;
using HackerOs.AppSdk.Globalization;
using Kazo.Chess.Interaction;
using Kazo.Chess.Model;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace Kazo.Chess.Components;

/// <summary>
/// The chessboard: an SVG drawn from a <see cref="GameSnapshot"/>, coloured by the OS theme tokens,
/// that turns mouse, touch and keyboard input into <see cref="MoveRequest"/>s. It knows no rule:
/// the legal moves it highlights come from the snapshot, and the move it raises is only played once
/// the window's rules accept it.
/// </summary>
/// <remarks>
/// Pointer input goes through <c>ChessBoard.razor.js</c>, which turns pointer coordinates into
/// square names and draws the dragged piece under the finger; every decision is taken here by
/// <see cref="BoardInteraction"/>.
/// </remarks>
public sealed partial class ChessBoard : IAsyncDisposable
{
    /// <summary>The pieces a pawn may become, in the order the picker shows them.</summary>
    private static readonly PieceType[] PromotionChoices = [PieceType.Queen, PieceType.Rook, PieceType.Bishop, PieceType.Knight];

    private readonly string _liveId = $"chess-board-live-{Guid.NewGuid():N}";
    private BoardInteraction _interaction = BoardInteraction.Initial;
    private IReadOnlySet<Square> _targets = new HashSet<Square>();
    private GameSnapshot? _renderedSnapshot;
    private ElementReference _host;
    private ElementReference _svg;
    private ElementReference _firstPromotionChoice;
    private FocusTarget _pendingFocus;
    private bool _disposed;
    private IJSObjectReference? _module;
    private IJSObjectReference? _binding;
    private DotNetObjectReference<ChessBoard>? _self;
    private IStringLocalizer<Strings>? _localizer;

    [Inject]
    private IJSRuntime JavaScript { get; set; } = default!;

    /// <summary>Gets or sets the execution context of the window that hosts the board (cascaded by the OS).</summary>
    [CascadingParameter]
    public IAppExecutionContext? AppContext { get; set; }

    /// <summary>Gets or sets the position to draw.</summary>
    [Parameter, EditorRequired]
    public GameSnapshot Snapshot { get; set; } = default!;

    /// <summary>Gets or sets the side shown at the bottom of the board.</summary>
    [Parameter]
    public PieceColor Orientation { get; set; } = PieceColor.White;

    /// <summary>Gets or sets the side the player may move now, or <see langword="null"/> to lock the board.</summary>
    [Parameter]
    public PieceColor? Mover { get; set; }

    /// <summary>Gets or sets the folder of the piece set under <c>wwwroot/pieces/</c>.</summary>
    [Parameter]
    public string PieceSet { get; set; } = "celtic";

    /// <summary>Gets or sets the callback that receives a move the player completed.</summary>
    [Parameter]
    public EventCallback<MoveRequest> OnMove { get; set; }

    /// <summary>
    /// Gets or sets the callback told when the board script could not load (pointer input is then
    /// unavailable; the keyboard still plays). It receives the browser's message.
    /// </summary>
    [Parameter]
    public EventCallback<string> OnScriptError { get; set; }

    // Where the keyboard focus goes after the next render: to the promotion picker when it opens,
    // back to the board when it closes, so a keyboard player never loses their place.
    private enum FocusTarget
    {
        None,
        PromotionPicker,
        Board,
    }

    private IStringLocalizer<Strings> L => _localizer ??= AppContext is { } context
        ? context.GetLocalizer<Strings>()
        : throw new InvalidOperationException("The chessboard must be hosted in the Chess window.");

    private string CursorDescription
    {
        get
        {
            Square cursor = _interaction.Cursor;
            string content = Snapshot.Board[cursor] is { } piece ? PieceName(piece) : L["EmptySquare"];
            string description = L["CursorDescription", cursor.Name, content];
            return _interaction.Selected is { } selected
                ? $"{description}. {L["SelectionDescription", selected.Name, _targets.Count]}"
                : description;
        }
    }

    /// <summary>Called by the board script when a pointer goes down on a square.</summary>
    /// <param name="square">The square's name.</param>
    [JSInvokable]
    public Task OnPress(string square) => RunFromScriptAsync(() =>
        Square.TryParse(square, out Square pressed)
            ? ApplyAsync(_interaction.Press(Snapshot, Mover, pressed))
            : Task.CompletedTask);

    /// <summary>Called by the board script when the pointer is released.</summary>
    /// <param name="square">The square under the pointer, or <see langword="null"/> off the board.</param>
    /// <param name="dragged">Whether the pointer moved far enough to be a drag.</param>
    [JSInvokable]
    public Task OnRelease(string? square, bool dragged) => RunFromScriptAsync(() =>
        ApplyAsync(_interaction.Release(Snapshot, Square.TryParse(square, out Square released) ? released : null, dragged)));

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await ReleaseScriptAsync();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        ArgumentNullException.ThrowIfNull(Snapshot);
        if (!ReferenceEquals(Snapshot, _renderedSnapshot))
        {
            // A new position (a move, a new game): whatever the hand held is dropped.
            _renderedSnapshot = Snapshot;
            _interaction = _interaction.Cleared();
        }

        _targets = _interaction.Targets(Snapshot);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await AttachScriptAsync();
        }

        FocusTarget focus = _pendingFocus;
        _pendingFocus = FocusTarget.None;
        if (focus == FocusTarget.PromotionPicker && _interaction.Promotion is not null)
        {
            await _firstPromotionChoice.FocusAsync();
        }
        else if (focus == FocusTarget.Board)
        {
            await _host.FocusAsync();
        }
    }

    // The window may close while the script loads: every step checks it, and whatever was created
    // after the close is released at once (no listener left on the board, no reference leaked).
    private async Task AttachScriptAsync()
    {
        _self = DotNetObjectReference.Create(this);
        try
        {
            _module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./_content/Kazo.Chess/Components/ChessBoard.razor.js");
            if (!_disposed)
            {
                _binding = await _module.InvokeAsync<IJSObjectReference>("attach", _host, _svg, _self);
            }
        }
        catch (JSException exception)
        {
            await OnScriptError.InvokeAsync(exception.Message);
        }

        if (_disposed)
        {
            await ReleaseScriptAsync();
        }
    }

    private async ValueTask ReleaseScriptAsync()
    {
        IJSObjectReference? binding = _binding;
        IJSObjectReference? module = _module;
        _binding = null;
        _module = null;
        try
        {
            if (binding is not null)
            {
                await binding.InvokeVoidAsync("detach");
                await binding.DisposeAsync();
            }

            if (module is not null)
            {
                await module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The page is gone: nothing left to detach.
        }
        finally
        {
            _self?.Dispose();
            _self = null;
        }
    }

    private async Task OnKeyDownAsync(KeyboardEventArgs args)
    {
        // Shortcuts with a modifier belong to the OS and the browser; a held key plays nothing twice.
        if (args.CtrlKey || args.AltKey || args.MetaKey || args.Repeat)
        {
            return;
        }

        if (_interaction.Promotion is not null)
        {
            await OnPromotionKeyAsync(args.Key);
            return;
        }

        switch (args.Key)
        {
            case "ArrowLeft":
                _interaction = _interaction.MoveCursor(-1, 0, Orientation);
                break;
            case "ArrowRight":
                _interaction = _interaction.MoveCursor(1, 0, Orientation);
                break;
            case "ArrowUp":
                _interaction = _interaction.MoveCursor(0, 1, Orientation);
                break;
            case "ArrowDown":
                _interaction = _interaction.MoveCursor(0, -1, Orientation);
                break;
            case "Enter" or " ":
                await ApplyAsync(_interaction.Activate(Snapshot, Mover));
                return;
            case "Escape":
                _interaction = _interaction.Cleared();
                break;
            default:
                return;
        }

        _targets = _interaction.Targets(Snapshot);
    }

    private Task OnPromotionKeyAsync(string key)
    {
        PieceType? choice = key.ToLowerInvariant() switch
        {
            "q" => PieceType.Queen,
            "r" => PieceType.Rook,
            "b" => PieceType.Bishop,
            "n" => PieceType.Knight,
            _ => null,
        };

        if (choice is { } piece)
        {
            return ChoosePromotionAsync(piece);
        }

        if (key == "Escape")
        {
            CancelPromotion();
        }

        return Task.CompletedTask;
    }

    private Task ChoosePromotionAsync(PieceType piece)
    {
        _pendingFocus = FocusTarget.Board;
        return ApplyAsync(_interaction.ChoosePromotion(piece));
    }

    private void CancelPromotion()
    {
        _interaction = _interaction.Cleared();
        _targets = _interaction.Targets(Snapshot);
        _pendingFocus = FocusTarget.Board;
    }

    private async Task ApplyAsync(InteractionResult result)
    {
        if (result.State.Promotion is not null && _interaction.Promotion is null)
        {
            _pendingFocus = FocusTarget.PromotionPicker;
        }

        _interaction = result.State;
        _targets = _interaction.Targets(Snapshot);
        if (result.Move is { } move)
        {
            await OnMove.InvokeAsync(move);
        }
    }

    // A script callback runs outside the app's ambient scope (ADR 0043 of HackerOS): enter it first,
    // so the work is the app's, then render like an ordinary UI event would.
    private async Task RunFromScriptAsync(Func<Task> work)
    {
        using IDisposable? scope = AppContext is { } context ? HackExecutionContext.Enter(context) : null;
        try
        {
            await work();
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private (int X, int Y) ToView(Square square) => Orientation == PieceColor.White
        ? (square.File, Square.Size - 1 - square.Rank)
        : (Square.Size - 1 - square.File, square.Rank);

    private Square FromView(int column, int row) => Orientation == PieceColor.White
        ? new Square(column, Square.Size - 1 - row)
        : new Square(Square.Size - 1 - column, row);

    private static string LabelTone(Square square) => square.IsDark ? "on-dark" : "on-light";

    private bool IsMovable(Piece piece) => Mover == Snapshot.Turn && piece.Color == Snapshot.Turn;

    private string PieceImage(Piece piece) => $"_content/Kazo.Chess/pieces/{PieceSet}/{piece.ImageName}.svg";

    private string PieceName(Piece piece) => L[$"Piece{piece.Color}{piece.Type}"];

    private string SquareClass(Square square)
    {
        var classes = new List<string>(5) { "square", square.IsDark ? "dark" : "light" };
        if (Snapshot.LastMove is { } last && (last.From == square || last.To == square))
        {
            classes.Add("last-move");
        }

        if (_interaction.Selected == square)
        {
            classes.Add("selected");
        }

        if (Snapshot.InCheck && Snapshot.Board.FindKing(Snapshot.Turn) == square)
        {
            classes.Add("check");
        }

        return string.Join(' ', classes);
    }
}
