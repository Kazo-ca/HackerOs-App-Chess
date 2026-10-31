using HackerOs.AppSdk;
using HackerOs.AppSdk.Globalization;
using Kazo.Chess.Engine;
using Kazo.Chess.Game;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;

namespace Kazo.Chess.Components;

/// <summary>What the player chose in the new game dialog.</summary>
/// <param name="Mode">Two players or against the computer.</param>
/// <param name="Level">The computer's level.</param>
/// <param name="Color">The side the player asked for against the computer.</param>
public sealed record NewGameChoice(GameMode Mode, EngineLevel Level, ColorChoice Color)
{
    /// <summary>Gets the choice shown the first time: two players, and Medium and White ready for the computer.</summary>
    public static NewGameChoice Default { get; } = new(GameMode.TwoPlayers, EngineLevel.Medium, ColorChoice.White);
}

/// <summary>
/// The "New game" dialog, drawn inside the window: who plays, and against the computer its level and
/// the player's side. It replaces a confirmation box: when a game is going on it says the game will
/// be abandoned, and Start is the confirmation.
/// </summary>
public sealed partial class NewGameDialog
{
    private readonly string _id = $"new-game-{Guid.NewGuid():N}";
    private GameMode _mode;
    private EngineLevel _level;
    private ColorChoice _color;
    private ElementReference _twoPlayersInput;
    private ElementReference _computerInput;
    private ElementReference _startButton;
    private IStringLocalizer<Strings>? _localizer;

    /// <summary>Gets or sets the execution context of the window that hosts the dialog (cascaded by the OS).</summary>
    [CascadingParameter]
    public IAppExecutionContext? AppContext { get; set; }

    /// <summary>Gets or sets the choice the dialog opens with (the last one made).</summary>
    [Parameter]
    public NewGameChoice Initial { get; set; } = NewGameChoice.Default;

    /// <summary>Gets or sets whether starting abandons a game in progress (the dialog then says so).</summary>
    [Parameter]
    public bool AbandonsGame { get; set; }

    /// <summary>Gets or sets the callback that receives the choice when the player starts.</summary>
    [Parameter]
    public EventCallback<NewGameChoice> OnStart { get; set; }

    /// <summary>Gets or sets the callback raised when the player closes the dialog without starting.</summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    private IStringLocalizer<Strings> L => _localizer ??= AppContext is { } context
        ? context.GetLocalizer<Strings>()
        : throw new InvalidOperationException("The new game dialog must be hosted in the Chess window.");

    /// <inheritdoc />
    protected override void OnInitialized() => (_mode, _level, _color) = Initial;

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await FocusFirstAsync();
        }
    }

    // The checked mode is where a keyboard player expects to land (and where arrows change it).
    private async Task FocusFirstAsync() =>
        await (_mode == GameMode.AgainstComputer ? _computerInput : _twoPlayersInput).FocusAsync();

    private async Task FocusLastAsync() => await _startButton.FocusAsync();

    private Task StartAsync() => OnStart.InvokeAsync(new NewGameChoice(_mode, _level, _color));

    private Task OnKeyDownAsync(KeyboardEventArgs args) =>
        args.Key == "Escape" ? OnCancel.InvokeAsync() : Task.CompletedTask;
}
