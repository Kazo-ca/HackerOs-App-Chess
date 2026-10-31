using HackerOs.AppSdk.Blazor;
using HackerOs.AppSdk.Globalization;
using Kazo.Chess.Model;
using Kazo.Chess.Rules;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace Kazo.Chess;

/// <summary>
/// The Chess window: one game at a time, two players on the same screen. The window owns the game
/// (an <see cref="IChessRules"/> over chess.js) and hands each new <see cref="GameSnapshot"/> to the
/// <see cref="Components.ChessBoard"/>, which raises the moves the player completes.
/// </summary>
public sealed partial class ChessWindow : IAsyncDisposable
{
    private IChessRules? _rules;
    private GameSnapshot? _snapshot;
    private PieceColor _orientation = PieceColor.White;
    private bool _busy;
    private string? _error;
    private IStringLocalizer<Strings>? _localizer;

    [Inject]
    private IJSRuntime JavaScript { get; set; } = default!;

    private IStringLocalizer<Strings> L => _localizer ??= AppContext.GetLocalizer<Strings>();

    // Two players share the screen: whoever's turn it is may move, unless a move is being played or
    // the game is over.
    private PieceColor? Mover => _snapshot is { IsOver: false } snapshot && !_busy ? snapshot.Turn : null;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_rules is not null)
        {
            await _rules.DisposeAsync();
        }
    }

    /// <inheritdoc />
    protected override async Task OnAppAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _rules = new ChessJsRules(JavaScript);
        await RunRulesAsync(async () => _snapshot = await _rules.NewGameAsync(cancellationToken: AppContext.CancellationToken));
        StateHasChanged();
    }

    private async Task NewGameAsync()
    {
        if (_rules is null || _snapshot is null || _busy)
        {
            return;
        }

        // Locked from the first click: the board and this button wait for the answer, so no move
        // is lost behind the dialog and no second dialog opens.
        _busy = true;
        try
        {
            if (_snapshot.History.Count > 0 && !_snapshot.IsOver)
            {
                MessageBoxDialogResult answer = await MessageBox(L["NewGame"], L["ConfirmNewGame"], MessageBoxType.YesNo);
                if (answer.Result != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            await RunRulesAsync(async () =>
            {
                _snapshot = await _rules.NewGameAsync(cancellationToken: AppContext.CancellationToken);
                _orientation = PieceColor.White;
            });
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task PlayAsync(MoveRequest move)
    {
        if (_rules is null || _busy)
        {
            return;
        }

        _busy = true;
        try
        {
            // An illegal move (a stale board) changes nothing: the board redraws the same position.
            await RunRulesAsync(async () =>
                _snapshot = await _rules.TryMoveAsync(move, AppContext.CancellationToken) ?? _snapshot);
        }
        finally
        {
            _busy = false;
        }
    }

    // Every call into the rules: a failure is shown in the window (the game stays as it was), and the
    // window closing mid-call (its token cancelled) is no error at all.
    private async Task RunRulesAsync(Func<Task> call)
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
            _error = L["RulesFailed", exception.Message];
        }
    }

    private void OnBoardScriptError(string message) => _error = L["PointerUnavailable", message];

    private string StatusText(GameStatus status) =>
        status.Side is { } side ? L[status.Key, SideName(side)] : L[status.Key];

    private string SideName(PieceColor side) => side == PieceColor.White ? L["SideWhite"] : L["SideBlack"];
}
