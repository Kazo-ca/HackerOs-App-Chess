using HackerOs.AppSdk.Blazor;
using HackerOs.AppSdk.Globalization;
using HackerOs.AppSdk.IO;
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
/// The Chess window. It shows the <see cref="GameSession"/> (the rules, Stockfish when the computer
/// plays, the clock) and forwards what the player does: the moves completed on the
/// <see cref="Components.ChessBoard"/>, undo, flip, and the choices of the <see cref="NewGameDialog"/>.
/// </summary>
public sealed partial class ChessWindow : IAsyncDisposable
{
    // How often a running clock is redrawn and checked for a fallen flag: tenths are shown below
    // ten seconds, so a tenth of a second is the face's resolution.
    private static readonly TimeSpan ClockTick = TimeSpan.FromMilliseconds(100);

    private GameSession _session = default!;
    private NewGameChoice _lastChoice = NewGameChoice.Default;
    private bool _dialogOpen;
    private bool _flipped;
    private bool _focusNewGameButton;
    private ElementReference _newGameButton;
    private string? _error;
    private string? _notice;
    private string? _documentName;
    private Timer? _clockTimer;
    private IStringLocalizer<Strings>? _localizer;

    [Inject]
    private IJSRuntime JavaScript { get; set; } = default!;

    private IStringLocalizer<Strings> L => _localizer ??= AppContext.GetLocalizer<Strings>();

    private GameSnapshot? Snapshot => _session.Snapshot;

    // The side at the bottom: the player's against the computer, White between two players;
    // "Flip board" turns it around until the next game.
    private PieceColor Orientation => _flipped ? _session.Setup.Orientation.Opponent() : _session.Setup.Orientation;

    // While the dialog is open the board waits: no move is lost behind it.
    private PieceColor? Mover => _dialogOpen ? null : _session.Mover;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_clockTimer is not null)
        {
            await _clockTimer.DisposeAsync();
        }

        _session.Changed -= OnSessionChanged;
        await _session.DisposeAsync();
    }

    /// <inheritdoc />
    protected override void OnAppInitialized()
    {
        _session = new GameSession(new ChessJsRules(JavaScript), () => new StockfishEngine(JavaScript));
        _session.Changed += OnSessionChanged;
        _clockTimer = new Timer(_ => _ = InvokeAsync(OnClockTick), null, ClockTick, ClockTick);
    }

    /// <inheritdoc />
    protected override async Task OnAppAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Opened on a .pgn file (a double-click, "Open with"): that game, otherwise a new one.
            if (LaunchFile is not { } file || !await LoadFileAsync(file.Value))
            {
                await RunGameAsync(() => _session.StartAsync(GameSetup.TwoPlayers, AppContext.CancellationToken));
            }

            // Nothing renders after OnAfterRender on its own: show the first game, or why it failed.
            StateHasChanged();
            return;
        }

        if (_focusNewGameButton)
        {
            // Back from the dialog: the keyboard focus returns where it was.
            _focusNewGameButton = false;
            await _newGameButton.FocusAsync();
        }
    }

    private void OpenNewGameDialog() => _dialogOpen = true;

    private void CloseNewGameDialog()
    {
        _dialogOpen = false;
        _focusNewGameButton = true;
    }

    private async Task StartNewGameAsync(NewGameChoice choice)
    {
        _lastChoice = choice;
        CloseNewGameDialog();
        _flipped = false;
        await SetDocumentAsync(null);
        GameSetup setup = choice.Mode == GameMode.AgainstComputer
            ? GameSetup.AgainstComputer(choice.Level, choice.Color, Random.Shared, choice.TimeControl)
            : GameSetup.TwoPlayers with { Clock = choice.TimeControl };
        await RunGameAsync(() => _session.StartAsync(setup, AppContext.CancellationToken));
    }

    // Open a saved game: the current one is abandoned only once the player confirms and the file reads.
    private async Task OpenPgnAsync()
    {
        if (_session.Snapshot is { History.Count: > 0 } && !_session.IsOver)
        {
            MessageBoxDialogResult answer = await MessageBox(L["Open"], L["ConfirmOpen"], MessageBoxType.YesNo);
            if (answer.Result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        OpenFileDialogResult result = await OpenFileAsync(new OpenFileDialogRequest
        {
            Title = L["OpenTitle"],
            Filters = [new FileDialogFilter(L["FilterPgn"], [".pgn"], ["application/x-chess-pgn"]), new FileDialogFilter(L["FilterAll"], [], [])],
            RequestedAccess = SelectedFileAccess.Read,
        });
        if (result.Status == FileDialogStatus.Selected && result.Resources.Count > 0)
        {
            await LoadFileAsync(result.Resources[0].Path.Value);
        }
    }

    private async Task SavePgnAsync()
    {
        SaveFileDialogResult result = await SaveFileAsync(new SaveFileDialogRequest
        {
            Title = L["SaveTitle"],
            SuggestedFileName = _documentName ?? $"chess-{Today:yyyy-MM-dd}.pgn",
            DefaultExtension = ".pgn",
            Filters = [new FileDialogFilter(L["FilterPgn"], [".pgn"], ["application/x-chess-pgn"])],
            ConfirmOverwrite = true,
        });
        if (result.Status != FileDialogStatus.Selected || result.Resource is not { } target)
        {
            return;
        }

        string path = target.Path.Value;
        string name = HackPath.GetFileName(path);
        try
        {
            string pgn = await _session.ExportPgnAsync(Today, AppContext.UserId, AppContext.CancellationToken);
            await HackFile.WriteAllTextAsync(path, pgn, AppContext.CancellationToken);
            await SetDocumentAsync(name);
            _error = null;
            _notice = L["Saved", name];
        }
        catch (Exception exception) when (exception is HackIOException or HackUnauthorizedAccessException)
        {
            _error = L["FileFailed", name, exception.Message];
        }
    }

    // Reads a .pgn file and opens its game; a file that does not read, or is not a game, leaves the
    // current game as it was and says why.
    private async Task<bool> LoadFileAsync(string path)
    {
        string name = HackPath.GetFileName(path);
        try
        {
            string pgn = await HackFile.ReadAllTextAsync(path, AppContext.CancellationToken);
            await _session.LoadAsync(pgn, AppContext.CancellationToken);
            _flipped = false;
            await SetDocumentAsync(name);
            _error = null;
            _notice = null;
            return true;
        }
        catch (FormatException exception)
        {
            _error = L["PgnInvalid", name, exception.Message];
        }
        catch (Exception exception) when (exception is HackIOException or HackUnauthorizedAccessException or JSException)
        {
            _error = L["FileFailed", name, exception.Message];
        }

        return false;
    }

    // "game.pgn — Chess" while a saved game is shown, the app's name otherwise (ADR 0117 of HackerOS).
    private async Task SetDocumentAsync(string? name)
    {
        _documentName = name;
        _notice = null;
        await SetWindowTitleAsync(name is null ? WindowTitleForApp : FormatWindowTitle(name, false));
    }

    private string WindowTitleForApp => L["AppTitle"];

    private DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    // The player's move, then the computer's reply when it plays; a refused move changes nothing.
    private Task PlayAsync(MoveRequest move) =>
        RunGameAsync(() => _session.PlayAsync(move, AppContext.CancellationToken));

    private Task UndoAsync() =>
        RunGameAsync(() => _session.UndoAsync(AppContext.CancellationToken));

    private void Flip() => _flipped = !_flipped;

    // Only a running clock needs redrawing; the session ends the game when a flag falls.
    private void OnClockTick()
    {
        if (_session.Clock?.Running is null)
        {
            return;
        }

        _session.CheckTime();
        StateHasChanged();
    }

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
        catch (JSDisconnectedException)
        {
            // The page is going away: there is no one left to tell.
        }
        catch (Exception exception) when (exception is JSException or FormatException or InvalidOperationException)
        {
            _error = L["GameFailed", exception.Message];
        }
    }

    // The session changes from its own awaits (the computer's move): render on the window's dispatcher.
    private void OnSessionChanged() => _ = InvokeAsync(StateHasChanged);

    private void OnBoardScriptError(string message) => _error = L["PointerUnavailable", message];

    private string StatusText(GameStatus status) => status switch
    {
        { Side: { } side } => L[status.Key, SideName(side)],
        { Detail: { } detail } => L[status.Key, detail],
        _ => L[status.Key],
    };

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
