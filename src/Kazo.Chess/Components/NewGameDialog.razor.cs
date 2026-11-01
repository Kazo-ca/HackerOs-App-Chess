using HackerOs.AppSdk;
using HackerOs.AppSdk.Globalization;
using Kazo.Chess.Clock;
using Kazo.Chess.Engine;
using Kazo.Chess.Game;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;

namespace Kazo.Chess.Components;

/// <summary>The clocks the dialog offers (plan, decision Q32).</summary>
public enum ClockChoice
{
    /// <summary>No clock.</summary>
    None,

    /// <summary>5 minutes per side, no increment.</summary>
    Five,

    /// <summary>10 minutes per side, no increment.</summary>
    Ten,

    /// <summary>15 minutes per side, 10 seconds per move.</summary>
    FifteenPlusTen,

    /// <summary>Minutes and increment of the player's choice.</summary>
    Custom,
}

/// <summary>What the player chose in the new game dialog.</summary>
/// <param name="Mode">Two players or against the computer.</param>
/// <param name="Level">The computer's level.</param>
/// <param name="Color">The side the player asked for against the computer.</param>
/// <param name="Clock">The clock.</param>
/// <param name="CustomMinutes">Minutes per side of a custom clock.</param>
/// <param name="CustomIncrementSeconds">Seconds per move of a custom clock.</param>
public sealed record NewGameChoice(
    GameMode Mode,
    EngineLevel Level,
    ColorChoice Color,
    ClockChoice Clock = ClockChoice.None,
    int CustomMinutes = 3,
    int CustomIncrementSeconds = 2)
{
    /// <summary>Gets the choice shown the first time: two players without a clock, and Medium and White ready for the computer.</summary>
    public static NewGameChoice Default { get; } = new(GameMode.TwoPlayers, EngineLevel.Medium, ColorChoice.White);

    /// <summary>Gets the time control chosen, or <see langword="null"/> for no clock. Custom values are kept in range.</summary>
    public TimeControl? TimeControl => Clock switch
    {
        ClockChoice.Five => Kazo.Chess.Clock.TimeControl.Presets[0],
        ClockChoice.Ten => Kazo.Chess.Clock.TimeControl.Presets[1],
        ClockChoice.FifteenPlusTen => Kazo.Chess.Clock.TimeControl.Presets[2],
        ClockChoice.Custom => Kazo.Chess.Clock.TimeControl.Of(
            Math.Clamp(CustomMinutes, Kazo.Chess.Clock.TimeControl.MinMinutes, Kazo.Chess.Clock.TimeControl.MaxMinutes),
            Math.Clamp(CustomIncrementSeconds, 0, Kazo.Chess.Clock.TimeControl.MaxIncrementSeconds)),
        _ => null,
    };
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
    private ClockChoice _clock;
    private int _customMinutes;
    private int _customIncrement;

    // The custom fields keep their value in range as it is typed, so what the field shows is what
    // the game gets (and what the dialog shows next time).
    private int CustomMinutes
    {
        get => _customMinutes;
        set => _customMinutes = Math.Clamp(value, TimeControl.MinMinutes, TimeControl.MaxMinutes);
    }

    private int CustomIncrement
    {
        get => _customIncrement;
        set => _customIncrement = Math.Clamp(value, 0, TimeControl.MaxIncrementSeconds);
    }
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
    protected override void OnInitialized() =>
        (_mode, _level, _color, _clock, _customMinutes, _customIncrement) = Initial;

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

    private Task StartAsync() =>
        OnStart.InvokeAsync(new NewGameChoice(_mode, _level, _color, _clock, _customMinutes, _customIncrement));

    private string ClockName(ClockChoice clock) => clock switch
    {
        ClockChoice.None => L["ClockNone"],
        ClockChoice.Custom => L["ClockCustom"],
        _ => (Initial with { Clock = clock }).TimeControl!.Label,
    };

    private Task OnKeyDownAsync(KeyboardEventArgs args) =>
        args.Key == "Escape" ? OnCancel.InvokeAsync() : Task.CompletedTask;
}
