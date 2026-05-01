// FILE: src/GreatEmailApp/Services/TrayNotifier.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 2
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Wraps an H.NotifyIcon.Wpf TaskbarIcon (purpose-built WPF tray library;
// chosen over <UseWindowsForms> to avoid namespace ambiguity with WPF/Drawing).
//
// The poller fires NewMailDetected events on a background thread; we coalesce
// events within a short window so a single poll cycle that surfaces 5 new
// messages doesn't flood the user with 5 separate balloons. One ballon →
// "5 new messages — Alice, Bob, …".

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using GreatEmailApp.Core.Notifications;
using H.NotifyIcon;
using H.NotifyIcon.Core;

namespace GreatEmailApp.Services;

public sealed class TrayNotifier : IDisposable
{
    private readonly INewMailPoller _poller;
    private readonly TaskbarIcon _icon;
    private readonly object _bufferLock = new();
    private readonly List<NewMailEvent> _buffered = new();
    private Timer? _coalesce;
    private static readonly TimeSpan CoalesceWindow = TimeSpan.FromSeconds(2);

    public TrayNotifier(INewMailPoller poller)
    {
        _poller = poller;

        _icon = new TaskbarIcon
        {
            ToolTipText = "The Great Email App",
        };

        // Try to use the running exe's icon. If extraction fails (e.g. running
        // out of bin/Debug with no .ico embedded), the tray will show the
        // generic placeholder — still functional.
        var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "GreatEmailApp.exe");
        try
        {
            _icon.IconSource = new System.Windows.Media.Imaging.BitmapImage(new Uri(iconPath));
        }
        catch { /* fall back to default */ }

        // Click → bring main window forward.
        _icon.LeftClickCommand = new SimpleRelayCommand(BringMainWindowToFront);
        _icon.NoLeftClickDelay = true;

        // Right-click context menu.
        var menu = new System.Windows.Controls.ContextMenu();
        menu.Items.Add(MakeMenu("Open",       (_, _) => BringMainWindowToFront()));
        menu.Items.Add(MakeMenu("Check now",  async (_, _) => await _poller.PollOnceAsync().ConfigureAwait(false)));
        menu.Items.Add(new System.Windows.Controls.Separator());
        menu.Items.Add(MakeMenu("Quit",       (_, _) => Application.Current?.Shutdown()));
        _icon.ContextMenu = menu;

        _poller.NewMailDetected += OnNewMail;
    }

    private static System.Windows.Controls.MenuItem MakeMenu(string header, RoutedEventHandler onClick)
    {
        var item = new System.Windows.Controls.MenuItem { Header = header };
        item.Click += onClick;
        return item;
    }

    // --------------------------------------------------------------------- //
    // Coalescing
    // --------------------------------------------------------------------- //

    private void OnNewMail(object? sender, NewMailEvent e)
    {
        lock (_bufferLock)
        {
            _buffered.Add(e);
            // Restart the debounce window on every new event so a steady drip
            // collapses into a single trailing notification.
            _coalesce?.Dispose();
            _coalesce = new Timer(_ => Flush(), state: null, dueTime: CoalesceWindow, period: Timeout.InfiniteTimeSpan);
        }
    }

    private void Flush()
    {
        List<NewMailEvent> batch;
        lock (_bufferLock)
        {
            if (_buffered.Count == 0) return;
            batch = new List<NewMailEvent>(_buffered);
            _buffered.Clear();
        }
        // ShowNotification needs the UI thread.
        Application.Current?.Dispatcher.BeginInvoke(new Action(() => ShowBalloon(batch)));
    }

    private void ShowBalloon(List<NewMailEvent> batch)
    {
        string title;
        string body;

        if (batch.Count == 1)
        {
            var e = batch[0];
            var from = string.IsNullOrWhiteSpace(e.Message.Sender) ? e.Message.SenderEmail : e.Message.Sender;
            title = $"{from} — {e.Account.EmailAddress}";
            body  = string.IsNullOrWhiteSpace(e.Message.Subject) ? "(no subject)" : e.Message.Subject;
        }
        else
        {
            var byAccount = batch.GroupBy(b => b.Account.EmailAddress)
                                 .Select(g => $"{g.Count()} in {g.Key}")
                                 .ToArray();
            var latest = batch.TakeLast(3).Select(b =>
                string.IsNullOrWhiteSpace(b.Message.Sender) ? b.Message.SenderEmail : b.Message.Sender).ToArray();
            title = $"{batch.Count} new messages";
            body  = string.Join(" · ", byAccount) + "\n" + string.Join(", ", latest);
        }

        _icon.ShowNotification(
            title: Truncate(title, 63),
            message: Truncate(body, 255),
            icon: NotificationIcon.None,
            largeIcon: false,
            sound: true);
    }

    private static string Truncate(string s, int max) =>
        string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..(max - 1)] + "…");

    // --------------------------------------------------------------------- //
    // Window activation
    // --------------------------------------------------------------------- //

    private static void BringMainWindowToFront()
    {
        var w = Application.Current?.MainWindow;
        if (w is null) return;
        Application.Current!.Dispatcher.Invoke(() =>
        {
            if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
            w.Show();
            w.Activate();
            w.Topmost = true;   // brief topmost flick to defeat focus-stealing prevention
            w.Topmost = false;
            w.Focus();
        });
    }

    public void Dispose()
    {
        _poller.NewMailDetected -= OnNewMail;
        _icon.Dispose();
        _coalesce?.Dispose();
    }

    // --------------------------------------------------------------------- //
    // Tiny ICommand for LeftClickCommand
    // --------------------------------------------------------------------- //

    private sealed class SimpleRelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _action;
        public SimpleRelayCommand(Action action) => _action = action;
        public bool CanExecute(object? p) => true;
        public void Execute(object? p) => _action();
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
