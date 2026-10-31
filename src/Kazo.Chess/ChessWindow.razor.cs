using HackerOs.AppSdk.Globalization;
using Kazo.Chess.Components;
using Kazo.Chess.Engine;
using Kazo.Chess.Game;
using Kazo.Chess.Model;
using Kazo.Chess.Rules;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace Kazo.Chess;

/// <summary>
/// The Chess window. It shows the <see cref="GameSession"/> (the rules, and Stockfish when the
/// computer plays) and forwards what the player does: the moves completed on the
/// <see cref="Components.ChessBoard"/> and the choices of the <see cref="NewGameDialog"/>.
/// </summary>
public sealed partial class ChessWindow : IAsyncDisposable
{
    private GameSession _session = default!;
    private NewGameChoice _lastChoice = NewGameChoice.Default;
    private bool _dialogOpen;
    private string? _error;
    private IStringLocalizer<Strings>? _localizer;

    [Inject]
    private IJSRuntime JavaScript { get; set; } = default!;

    private IStringLocalizer<Strings> L => _localizer ??= AppContext.GetLocalizer<Strings>();

    private GameSnapshot? Snapshot => _session.Snapshot;

    // While the dialog is open the board waits: no move is lost behind it.
    private PieceColor? Mover => _dialogOpen ? null : _session.Mover;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _session.Changed -= OnSessionChanged;
        await _session.DisposeAsync();
    }

    /// <inheritdoc />
    protected override void OnAppInitialized()
    {
        _session = new GameSession(new ChessJsRules(JavaScript), () => new StockfishEngine(JavaScript));
        _session.Changed += OnSessionChanged;
    }

    /// <inheritdoc />
    protected override async Task OnAppAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await RunGameAsync(() => _session.StartAsync(GameSetup.TwoPlayers, AppContext.CancellationToken));
        }
    }

    private void OpenNewGameDialog() => _dialogOpen = true;

    private void CloseNewGameDialog() => _dialogOpen = false;

    private Task StartNewGameAsync(NewGameChoice choice)
    {
        _lastChoice = choice;
        _dialogOpen = false;
        GameSetup setup = choice.Mode == GameMode.AgainstComputer
            ? GameSetup.AgainstComputer(choice.Level, choice.Color, Random.Shared)
            : GameSetup.TwoPlayers;
        return RunGameAsync(() => _session.StartAsync(setup, AppContext.CancellationToken));
    }

    // The player's move, then the computer's reply when it plays; a refused move changes nothing.
    private Task PlayAsync(MoveRequest move) =>
        RunGameAsync(() => _session.PlayAsync(move, AppContext.CancellationToken));

    // Every call into the game: a failure is shown in the window (the game stays as it was), and the
    // window closing mid-call (its token cancelled) is no error at all.
    private async Task RunGameAsync(Func<Task> call)
    {
        try
        {
            await call();
            _error = null;
        }
        catch (OperationCanceledException) when (AppContext.CancellationToken.IsCancellationRequested)
        {
            // The window is closing.
        }
        catch (Exception exception) when (exception is JSException or FormatException or InvalidOperationException)
        {
            _error = L["GameFailed", exception.Message];
        }
    }

    // The session changes from its own awaits (the computer's move): render on the window's dispatcher.
    private void OnSessionChanged() => _ = InvokeAsync(StateHasChanged);

    private void OnBoardScriptError(string message) => _error = L["PointerUnavailable", message];

    private string StatusText(GameStatus status) =>
        status.Side is { } side ? L[status.Key, SideName(side)] : L[status.Key];

    private string SideName(PieceColor side) => side == PieceColor.White ? L["SideWhite"] : L["SideBlack"];

    private string PlayerName(PieceColor side)
    {
        GameSetup setup = _session.Setup;
        if (setup.Mode == GameMode.TwoPlayers)
        {
            return L["PlayerHuman"];
        }

        return side == setup.ComputerColor
            ? L["PlayerComputer", L[$"Level{setup.Level}"]]
            : L["PlayerYou"];
    }
}
