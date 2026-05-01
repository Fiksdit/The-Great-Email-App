// FILE: src/GreatEmailApp/Controls/TitleBar.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 2
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
        // Phase 1: avatar popover lands in Phase 4 (Firebase). For now, show a quick info popup.
        MessageBox.Show(
            $"Signed in: {AccountEmail}\nSync: on\n\n(Avatar popover with Sign Out arrives in Phase 4.)",
            "Account",
            MessageBoxButton.OK, MessageBoxImage.Information);
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
