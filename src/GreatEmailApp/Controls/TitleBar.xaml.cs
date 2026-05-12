// FILE: src/GreatEmailApp/Controls/TitleBar.xaml.cs
// Created: 2026-04-29 | Revised: 2026-05-12 | Rev: 4
// Changed by: Claude Opus 4.7 on behalf of James Reed

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
        SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;

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
