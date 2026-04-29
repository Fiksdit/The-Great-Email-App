// FILE: src/GreatEmailApp/Controls/TitleBar.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
}
