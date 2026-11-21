// FILE: src/GreatEmailApp/Controls/TitleBar.xaml.cs
// Created: 2026-04-29 | Revised: 2026-11-21 | Rev: 7
// Changed by: Claude Opus 5.5 on behalf of James Reed

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GreatEmailApp.Core.Search;
using GreatEmailApp.Core.Services;
using GreatEmailApp.ViewModels;

namespace GreatEmailApp.Controls;

public partial class TitleBar : UserControl
{
    public TitleBar()
    {
        InitializeComponent();
        MouseLeftButtonDown += OnDragArea;
        Loaded += OnLoadedSignInHint;
    }

    // --------------------------------------------------------------------- //
    // New-PC setup callout (two steps)
    //   Step 1 — signed out after startup re-auth: sign in with Google.
    //   Step 2 — signed in, but the password vault (rulebook §7C) exists and is
    //            still locked on this PC: unlock it with the master passphrase
    //            so IMAP passwords restore. Easy to forget — it's a separate
    //            action after sign-in, in Settings → Sync.
    // --------------------------------------------------------------------- //

    private enum HintStep { None, SignIn, UnlockPasswords }

    // The step that should be showing. The popup itself is only open while the
    // main window is active and not minimized — WPF popups don't follow their
    // owner, so without this the callout would float over other apps or stay
    // put when the window moves.
    private HintStep _hintStep = HintStep.None;
    private bool _signInHintDismissed;
    private bool _unlockHintDismissed;

    private async void OnLoadedSignInHint(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoadedSignInHint;
        if (App.Auth is null) return;

        var w = Window.GetWindow(this);
        if (w is not null)
        {
            w.LocationChanged += (_, _) => RepositionSignInHint();
            w.SizeChanged     += (_, _) => RepositionSignInHint();
            w.StateChanged    += (_, _) => UpdateSignInHint();
            w.Activated       += (_, _) => UpdateSignInHint();
            w.Deactivated     += (_, _) => UpdateSignInHint();
        }

        // Signing in moves on to step 2 (if the vault is locked); signing out
        // mid-session just hides the callout — step 1 is a startup-only nudge.
        App.Auth.SessionChanged += (_, _) => Dispatcher.BeginInvoke(new Action(async () =>
        {
            if (App.Auth.IsSignedIn) await EvaluateSetupHintAsync();
            else SetHintStep(HintStep.None);
        }));

        // Wait for the startup silent re-auth so step 1 never flashes on a PC
        // that already has a saved session.
        try { await App.AuthRestoreCompleted; } catch { /* failed restore == signed out */ }
        await EvaluateSetupHintAsync();
    }

    private async Task EvaluateSetupHintAsync()
    {
        if (App.Auth is null) return;
        if (!App.Auth.IsSignedIn)
        {
            SetHintStep(_signInHintDismissed ? HintStep.None : HintStep.SignIn);
            return;
        }

        SetHintStep(HintStep.None);
        if (App.Vault is null || App.Vault.IsUnlocked || _unlockHintDismissed) return;

        // One Firestore GET: does a vault exist for this user? (NotSetUp → no
        // step 2; there's nothing to restore.) Off the UI thread per ProbeAsync.
        Result<GreatEmailApp.Core.Crypto.VaultStatus> probe;
        try { probe = await Task.Run(() => App.Vault.ProbeAsync()); }
        catch { return; }

        // State may have moved on while the probe was in flight.
        if (!App.Auth.IsSignedIn || App.Vault.IsUnlocked || _unlockHintDismissed) return;
        if (probe is Result<GreatEmailApp.Core.Crypto.VaultStatus>.Ok { Value: GreatEmailApp.Core.Crypto.VaultStatus.Locked })
            SetHintStep(HintStep.UnlockPasswords);
    }

    private void SetHintStep(HintStep step)
    {
        _hintStep = step;
        switch (step)
        {
            case HintStep.SignIn:
                HintStepText.Text = "Step 1 of 2";
                HintTitleText.Text = "Sign in to sync";
                HintBodyText.Text = "Click this button to sign in with Google. Your accounts, settings and " +
                                    "saved passwords will download from your other PCs.";
                HintActionButton.Content = "Sign in";
                System.Windows.Automation.AutomationProperties.SetName(HintActionButton, "Sign in with Google");
                break;
            case HintStep.UnlockPasswords:
                HintStepText.Text = "Step 2 of 2";
                HintTitleText.Text = "Restore your passwords";
                HintBodyText.Text = "Your accounts synced. To restore their saved passwords on this PC, unlock " +
                                    "password sync with the master passphrase you set on your other PC.";
                HintActionButton.Content = "Unlock";
                System.Windows.Automation.AutomationProperties.SetName(HintActionButton, "Unlock password sync");
                break;
        }
        UpdateSignInHint();
    }

    private void UpdateSignInHint()
    {
        // Unlocked elsewhere (e.g. directly in Settings → Sync) → step 2 is done.
        if (_hintStep == HintStep.UnlockPasswords && (App.Vault?.IsUnlocked ?? false))
            _hintStep = HintStep.None;

        var w = Window.GetWindow(this);
        SignInHintPopup.IsOpen = _hintStep != HintStep.None
            && w is not null && w.IsActive && w.WindowState != WindowState.Minimized
            && (IsForeground(w) || PopupIsForeground());

        // WPF can report IsActive=true for a window Windows kept behind another
        // app (launched while the user was typing elsewhere — foreground lock),
        // and no Activated event fires when it later comes to the front. Poll
        // the real foreground window while a step is pending to cover that.
        if (_hintStep != HintStep.None)
        {
            if (_hintForegroundTimer is null)
            {
                _hintForegroundTimer = new System.Windows.Threading.DispatcherTimer
                    { Interval = TimeSpan.FromSeconds(1) };
                _hintForegroundTimer.Tick += (_, _) => UpdateSignInHint();
            }
            _hintForegroundTimer.Start();
        }
        else
        {
            _hintForegroundTimer?.Stop();
        }
    }

    private System.Windows.Threading.DispatcherTimer? _hintForegroundTimer;

    private static bool IsForeground(Window w)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(w).Handle;
        return hwnd != IntPtr.Zero && GetForegroundWindow() == hwnd;
    }

    // Clicking a button inside the callout can hand foreground to the popup's
    // own HWND — that must not count as "app is in the background".
    private bool PopupIsForeground()
    {
        if (!SignInHintPopup.IsOpen || SignInHintPopup.Child is null) return false;
        return System.Windows.PresentationSource.FromVisual(SignInHintPopup.Child)
                   is System.Windows.Interop.HwndSource src
               && src.Handle == GetForegroundWindow();
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    // Nudging the offset forces WPF to recompute the popup's screen position.
    private void RepositionSignInHint()
    {
        if (!SignInHintPopup.IsOpen) return;
        var o = SignInHintPopup.HorizontalOffset;
        SignInHintPopup.HorizontalOffset = o + 1;
        SignInHintPopup.HorizontalOffset = o;
    }

    private void SignInHintDismiss_Click(object sender, RoutedEventArgs e)
    {
        if (_hintStep == HintStep.SignIn) _signInHintDismissed = true;
        if (_hintStep == HintStep.UnlockPasswords) _unlockHintDismissed = true;
        SetHintStep(HintStep.None);
    }

    private void SignInHintAction_Click(object sender, RoutedEventArgs e)
    {
        if (_hintStep == HintStep.SignIn)
        {
            AvatarButton_Click(AvatarButton, e);
            return;
        }

        // Step 2: Settings → Sync holds the "Unlock with passphrase" button
        // (SettingsViewModel owns the unlock + ReloadAccounts flow — reuse it).
        SetHintStep(HintStep.None);
        var dlg = new Views.Dialogs.SettingsDialog { Owner = Window.GetWindow(this) };
        dlg.OpenOnTab("Sync");
        dlg.ShowDialog();
        if (!(App.Vault?.IsUnlocked ?? false) && !_unlockHintDismissed)
            SetHintStep(HintStep.UnlockPasswords);
    }

    public static readonly DependencyProperty AccountInitialProperty =
        DependencyProperty.Register(nameof(AccountInitial), typeof(string), typeof(TitleBar),
            new PropertyMetadata("?"));
    public string AccountInitial
    {
        get => (string)GetValue(AccountInitialProperty);
        set => SetValue(AccountInitialProperty, value);
    }

    public static readonly DependencyProperty AccountEmailProperty =
        DependencyProperty.Register(nameof(AccountEmail), typeof(string), typeof(TitleBar),
            new PropertyMetadata(""));
    public string AccountEmail
    {
        get => (string)GetValue(AccountEmailProperty);
        set => SetValue(AccountEmailProperty, value);
    }

    // Sync chip — bound from MainWindow.xaml to MainViewModel.SyncIndicator*. Hidden
    // until the user signs in; the visible label tracks SyncCoordinator state
    // (Sync on / Syncing… / Sync error) so it never lies about live sync.
    public static readonly DependencyProperty SyncChipVisibleProperty =
        DependencyProperty.Register(nameof(SyncChipVisible), typeof(bool), typeof(TitleBar),
            new PropertyMetadata(false));
    public bool SyncChipVisible
    {
        get => (bool)GetValue(SyncChipVisibleProperty);
        set => SetValue(SyncChipVisibleProperty, value);
    }

    public static readonly DependencyProperty SyncChipTextProperty =
        DependencyProperty.Register(nameof(SyncChipText), typeof(string), typeof(TitleBar),
            new PropertyMetadata("Sync on"));
    public string SyncChipText
    {
        get => (string)GetValue(SyncChipTextProperty);
        set => SetValue(SyncChipTextProperty, value);
    }

    public static readonly DependencyProperty SyncChipBrushProperty =
        DependencyProperty.Register(nameof(SyncChipBrush), typeof(System.Windows.Media.Brush), typeof(TitleBar),
            new PropertyMetadata(null));
    public System.Windows.Media.Brush? SyncChipBrush
    {
        get => (System.Windows.Media.Brush?)GetValue(SyncChipBrushProperty);
        set => SetValue(SyncChipBrushProperty, value);
    }

    public static readonly DependencyProperty SyncChipTooltipProperty =
        DependencyProperty.Register(nameof(SyncChipTooltip), typeof(string), typeof(TitleBar),
            new PropertyMetadata(""));
    public string SyncChipTooltip
    {
        get => (string)GetValue(SyncChipTooltipProperty);
        set => SetValue(SyncChipTooltipProperty, value);
    }

    private void OnDragArea(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        var w = Window.GetWindow(this);
        if (w is null) return;

        if (e.ClickCount == 2)
        {
            w.WindowState = w.WindowState == WindowState.Maximized
                ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        try { w.DragMove(); } catch { /* DragMove throws if not the left button */ }
    }

    private void Min_Click(object sender, RoutedEventArgs e)
    {
        var w = Window.GetWindow(this);
        if (w is not null) w.WindowState = WindowState.Minimized;
    }

    private void Max_Click(object sender, RoutedEventArgs e)
    {
        var w = Window.GetWindow(this);
        if (w is null) return;
        w.WindowState = w.WindowState == WindowState.Maximized
            ? WindowState.Normal : WindowState.Maximized;
        // Update glyph
        if (Resources["FontIcons"] is null) { /* nothing */ }
        var maxKey = w.WindowState == WindowState.Maximized ? "IconRestore" : "IconMax";
        if (Application.Current.Resources[maxKey] is string glyph)
            MaxGlyph.Text = glyph;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        var w = Window.GetWindow(this);
        w?.Close();
    }

    private void AvatarButton_Click(object sender, RoutedEventArgs e)
    {
        // Clicking the avatar is the action step 1 of the setup callout asks
        // for, so it counts as acknowledging it. (Step 2 stays — signing in
        // here moves the callout on to "Restore your passwords".)
        if (_hintStep == HintStep.SignIn)
        {
            _signInHintDismissed = true;
            SetHintStep(HintStep.None);
        }

        // Signed-out: jump straight to Settings → Sync, where the Google sign-in
        // button lives. Avoids the old "fake signed-in" popup that showed a
        // hardcoded email when no account was actually connected.
        if (!(App.Auth?.IsSignedIn ?? false))
        {
            var dlg = new Views.Dialogs.SettingsDialog { Owner = Window.GetWindow(this) };
            dlg.OpenOnTab("Sync");
            dlg.ShowDialog();
            return;
        }

        // Signed-in: open a small popover menu under the avatar. The menu shows
        // the signed-in email (read-only header) plus Sign out and a shortcut
        // to Settings → Sync. ContextMenu's `Placement=Bottom` aligns it under
        // the avatar so it reads as a popover, not a context menu.
        var menu = new ContextMenu
        {
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            PlacementTarget = AvatarButton,
            StaysOpen = false,
        };

        var header = new MenuItem
        {
            Header = $"Signed in as {AccountEmail}",
            IsEnabled = false,
        };
        menu.Items.Add(header);

        menu.Items.Add(new Separator());

        var syncSettings = new MenuItem { Header = "Sync settings…" };
        syncSettings.Click += (_, _) =>
        {
            var dlg = new Views.Dialogs.SettingsDialog { Owner = Window.GetWindow(this) };
            dlg.OpenOnTab("Sync");
            dlg.ShowDialog();
        };
        menu.Items.Add(syncSettings);

        var signOut = new MenuItem { Header = "Sign out" };
        signOut.Click += async (_, _) => await SignOutAsync();
        menu.Items.Add(signOut);

        menu.IsOpen = true;
    }

    private async Task SignOutAsync()
    {
        if (App.Auth is null) return;
        try
        {
            var res = await App.Auth.SignOutAsync();
            // SignOutAsync clears the session and fires SessionChanged. The
            // MainViewModel handler in App.xaml.cs reacts and the avatar
            // repaints to "?"/"Sign in" automatically — no manual UI update
            // needed here. If the call itself failed, surface the error.
            if (res is Result<bool>.Fail f)
            {
                MessageBox.Show(Window.GetWindow(this),
                    $"Sign out failed: {f.Error}", "Sign out",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this),
                $"Sign out error: {ex.Message}", "Sign out",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    // --------------------------------------------------------------------- //
    // Search
    // --------------------------------------------------------------------- //

    private CancellationTokenSource? _searchCts;
    private DispatcherTimerWrapper? _debounce;

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Placeholder visibility is now driven entirely by the XAML
        // MultiDataTrigger on SearchPlaceholder (empty text AND no focus).
        // Removed the imperative set here so the trigger isn't fought over.

        // Debounce: 250ms after the last keystroke, run the query.
        _debounce ??= new DispatcherTimerWrapper(TimeSpan.FromMilliseconds(250), RunSearch);
        _debounce.Restart();
    }

    private async void RunSearch()
    {
        var query = SearchBox.Text;
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        if (string.IsNullOrWhiteSpace(query))
        {
            ResultsPopup.IsOpen = false;
            return;
        }

        var result = await App.MessageCache.SearchAsync(query, limit: 30, ct);
        if (ct.IsCancellationRequested) return;

        var hits = (result is Result<System.Collections.Generic.List<SearchHit>>.Ok ok)
            ? ok.Value : new System.Collections.Generic.List<SearchHit>();

        ResultsList.ItemsSource = hits;
        ResultsEmpty.Visibility = hits.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResultsList.Visibility  = hits.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ResultsPopup.IsOpen = true;
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Down && ResultsList.Items.Count > 0)
        {
            ResultsList.Focus();
            ResultsList.SelectedIndex = 0;
            (ResultsList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && ResultsList.Items.Count > 0)
        {
            ResultsList.SelectedIndex = 0;
            ActivateSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SearchBox.Text = "";
            ResultsPopup.IsOpen = false;
            e.Handled = true;
        }
    }

    private void ResultsList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ActivateSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            SearchBox.Text = "";
            ResultsPopup.IsOpen = false;
            e.Handled = true;
        }
    }

    private void ResultsList_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ActivateSelected();

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        // The popup itself can take focus; only close if neither the box nor the
        // popup has focus a tick later.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!SearchBox.IsKeyboardFocusWithin && !ResultsList.IsKeyboardFocusWithin)
                ResultsPopup.IsOpen = false;
        }));
    }

    private void ActivateSelected()
    {
        if (ResultsList.SelectedItem is not SearchHit hit) return;
        ResultsPopup.IsOpen = false;
        SearchBox.Text = "";
        if (Window.GetWindow(this)?.DataContext is MainViewModel mvm)
            _ = mvm.NavigateToMessageAsync(hit.AccountId, hit.FolderPath, hit.Uid);
    }

    /// <summary>Tiny System.Windows.Threading.DispatcherTimer wrapper for restartable debounce.</summary>
    private sealed class DispatcherTimerWrapper
    {
        private readonly System.Windows.Threading.DispatcherTimer _t;
        public DispatcherTimerWrapper(TimeSpan interval, Action onTick)
        {
            _t = new System.Windows.Threading.DispatcherTimer { Interval = interval };
            _t.Tick += (_, _) => { _t.Stop(); onTick(); };
        }
        public void Restart() { _t.Stop(); _t.Start(); }
    }
}
