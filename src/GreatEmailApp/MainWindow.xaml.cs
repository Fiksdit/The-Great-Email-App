// FILE: src/GreatEmailApp/MainWindow.xaml.cs
// Created: 2026-04-29 | Revised: 2026-06-12 | Rev: 2
// Changed by: Claude Opus 4.8 on behalf of James Reed

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using GreatEmailApp.Core.Models;
using GreatEmailApp.ViewModels;
using GreatEmailApp.Views;

namespace GreatEmailApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        StateChanged += OnStateChanged;
        // Pull-on-focus: whenever the user comes back to the window, ask the
        // coordinator if there's anything new on Firestore. Cooldown lives in
        // the coordinator so this is safe to fire on every activation.
        Activated += (_, _) => App.SyncCoordinator?.OnWindowActivated();
        // Global keyboard shortcuts. Bubbling KeyDown (not Preview) so an
        // editable control — the search box, compose fields — consumes keys
        // like Delete first; we only act on keys that reach the window unhandled.
        KeyDown += OnKeyDown;
    }

    // --- Keyboard shortcuts (P1-11) ------------------------------------- //

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        switch (e.Key)
        {
            // F5 — force a Send/Receive poll now.
            case Key.F5:
                vm.SendReceiveCommand.Execute(null);
                e.Handled = true;
                break;

            // Delete — move the selected message to Trash. Explicitly bail when
            // a text box has focus: an empty search box doesn't mark Delete as
            // handled, so without this guard a stray Delete while editing could
            // nuke the selected mail.
            case Key.Delete:
                if (Keyboard.FocusedElement is System.Windows.Controls.TextBox) break;
                if (vm.SelectedMessage is not null)
                {
                    vm.DeleteCommand.Execute(vm.SelectedMessage);
                    e.Handled = true;
                }
                break;

            // Ctrl+R — reply to the selected message.
            case Key.R when Keyboard.Modifiers == ModifierKeys.Control:
                OpenReply(vm);
                e.Handled = true;
                break;

            // Ctrl+Shift+M — new message.
            case Key.M when Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift):
                OpenNewMessage(vm);
                e.Handled = true;
                break;
        }
    }

    private void OpenReply(MainViewModel vm)
    {
        if (vm.SelectedMessage?.Model is not Message msg) return;
        var accounts = App.Accounts.LoadAll();
        if (accounts.Count == 0) return;
        var win = ComposeWindow.OpenReply(accounts, DefaultAccount(accounts, msg.AccountId), msg, replyAll: false);
        win.Owner = this;
        win.Show();
    }

    private void OpenNewMessage(MainViewModel vm)
    {
        var accounts = App.Accounts.LoadAll();
        if (accounts.Count == 0)
        {
            vm.StatusMessage = "Add an account before composing.";
            return;
        }
        var win = ComposeWindow.OpenNew(accounts, DefaultAccount(accounts, vm.SelectedMessage?.Model.AccountId));
        win.Owner = this;
        win.Show();
    }

    // Mirror Ribbon.DefaultAccount: prefer the message's own account, then the
    // primary, then whatever's first.
    private static Account? DefaultAccount(IReadOnlyList<Account> accounts, string? preferAccountId)
    {
        if (!string.IsNullOrEmpty(preferAccountId))
        {
            var match = accounts.FirstOrDefault(a => a.Id == preferAccountId);
            if (match is not null) return match;
        }
        return accounts.FirstOrDefault(a => a.IsPrimary) ?? accounts.FirstOrDefault();
    }

    // NOTE: with WindowStyle=None, Windows still wants the chrome metrics. Hooking WM_GETMINMAXINFO
    // ensures Maximize doesn't cover the taskbar. Standard Windows pattern.
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        // When maximized with WindowStyle=None, add a small inset so the window
        // doesn't extend past monitor edges.
        if (WindowState == WindowState.Maximized)
        {
            BorderThickness = new Thickness(7);
        }
        else
        {
            BorderThickness = new Thickness(0);
        }
    }

    private const int WM_GETMINMAXINFO = 0x0024;

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_GETMINMAXINFO)
        {
            WmGetMinMaxInfo(hwnd, lParam);
            handled = true;
        }
        return IntPtr.Zero;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [DllImport("user32")]
    private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);
    [DllImport("user32")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

    private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
    {
        var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
        var mon = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (mon != IntPtr.Zero)
        {
            var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            GetMonitorInfo(mon, ref mi);
            var rcWork = mi.rcWork;
            var rcMon = mi.rcMonitor;
            mmi.ptMaxPosition.X = Math.Abs(rcWork.left - rcMon.left);
            mmi.ptMaxPosition.Y = Math.Abs(rcWork.top - rcMon.top);
            mmi.ptMaxSize.X = Math.Abs(rcWork.right - rcWork.left);
            mmi.ptMaxSize.Y = Math.Abs(rcWork.bottom - rcWork.top);
            Marshal.StructureToPtr(mmi, lParam, true);
        }
    }
}
