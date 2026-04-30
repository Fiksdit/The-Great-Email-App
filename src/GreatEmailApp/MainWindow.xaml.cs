// FILE: src/GreatEmailApp/MainWindow.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 2
// Changed by: Claude Sonnet 4.6 on behalf of James Reed

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace GreatEmailApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        StateChanged += OnStateChanged;

        // Show first-run sign-in overlay if this is the first launch.
        if (!App.Settings.HasShownFirstRun)
        {
            FirstRunOverlay.Visibility = Visibility.Visible;
            FirstRunOverlay.Dismissed += (_, _) =>
                FirstRunOverlay.Visibility = Visibility.Collapsed;
        }
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
