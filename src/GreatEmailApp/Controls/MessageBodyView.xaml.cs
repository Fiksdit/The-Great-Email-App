// FILE: src/GreatEmailApp/Controls/MessageBodyView.xaml.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GreatEmailApp.Core.Storage;
using GreatEmailApp.ViewModels;
using Microsoft.Web.WebView2.Core;

namespace GreatEmailApp.Controls;

/// <summary>
/// Email body renderer.
///
/// <para><b>Why WebView2 instead of a XAML HTML control:</b> real email HTML is
/// dirty — table layouts, inline styles, MSO comments — and trying to parse it
/// into a XAML tree gives you "your email is broken" complaints forever. Edge's
/// renderer handles all of it correctly, and WebView2 ships on every modern
/// Windows install.</para>
///
/// <para><b>Security stance per rulebook §11:</b>
/// <list type="bullet">
///   <item>JavaScript disabled.</item>
///   <item>External http(s) requests blocked unless the user opts in
///         (per-message via the banner, or globally via Settings → Security).</item>
///   <item>Link clicks open in the system browser, never in-place.</item>
///   <item>Dev tools / context menu / status bar all disabled.</item>
/// </list></para>
/// </summary>
public partial class MessageBodyView : UserControl
{
    public static readonly DependencyProperty MessageProperty =
        DependencyProperty.Register(nameof(Message), typeof(MessageViewModel), typeof(MessageBodyView),
            new PropertyMetadata(null, OnMessageChanged));

    public MessageViewModel? Message
    {
        get => (MessageViewModel?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    private bool _coreReady;
    private bool _allowImagesForThisMessage;
    private string _pendingHtml = "";
    private bool _initialNavigationConsumed;

    public MessageBodyView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_coreReady) return;
        try
        {
            // Per-user data folder — keeps cookies/cache out of Program Files and
            // shared with our other LOCALAPPDATA state.
            var userDataFolder = Path.Combine(AppPaths.Root, "WebView2Data");
            Directory.CreateDirectory(userDataFolder);
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await Web.EnsureCoreWebView2Async(env);

            var s = Web.CoreWebView2.Settings;
            s.IsScriptEnabled = false;
            s.AreDefaultContextMenusEnabled = false;
            s.AreDevToolsEnabled = false;
            s.IsStatusBarEnabled = false;
            s.AreHostObjectsAllowed = false;
            s.IsZoomControlEnabled = true;

            // Filter EVERY request so we can block external resources before they fly.
            Web.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            Web.CoreWebView2.WebResourceRequested += OnWebResourceRequested;
            Web.CoreWebView2.NavigationStarting += OnNavigationStarting;
            Web.CoreWebView2.NewWindowRequested += OnNewWindowRequested;

            _coreReady = true;
            if (!string.IsNullOrEmpty(_pendingHtml))
                LoadHtml(_pendingHtml);
        }
        catch (Exception ex)
        {
            // WebView2 runtime missing or corrupt. Show a textual fallback.
            Web.Visibility = Visibility.Collapsed;
            BlockedBanner.Visibility = Visibility.Visible;
            BlockedBanner.Background = System.Windows.Media.Brushes.DarkRed;
            ShowImagesButton.Visibility = Visibility.Collapsed;
            ((TextBlock)((DockPanel)BlockedBanner.Child).Children[1]).Text =
                $"WebView2 runtime unavailable: {ex.Message}. " +
                "Install Microsoft Edge WebView2 Runtime to render email bodies.";
        }
    }

    // --------------------------------------------------------------------- //
    // Body wiring
    // --------------------------------------------------------------------- //

    private static void OnMessageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (MessageBodyView)d;
        view._allowImagesForThisMessage = false;
        view.BlockedBanner.Visibility = Visibility.Collapsed;
        view._initialNavigationConsumed = false;
        view.Render();
    }

    private void Render()
    {
        var msg = Message;
        if (msg is null)
        {
            LoadHtml(WrapPlainText(""));
            return;
        }

        var preferHtml = App.Settings.ShowHtml && !string.IsNullOrEmpty(msg.BodyHtml);
        var html = preferHtml
            ? WrapHtmlBody(msg.BodyHtml)
            : WrapPlainText(!string.IsNullOrEmpty(msg.BodyPlain) ? msg.BodyPlain : msg.BodyHtml);

        LoadHtml(html);
    }

    private void LoadHtml(string html)
    {
        _pendingHtml = html;
        if (!_coreReady) return;
        _initialNavigationConsumed = false;
        Web.NavigateToString(html);
    }

    public void Refresh() => Render();

    // --------------------------------------------------------------------- //
    // Resource and navigation gating
    // --------------------------------------------------------------------- //

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        var uri = e.Request.Uri ?? "";

        // The initial NavigateToString turns into "data:text/html,..." or about:blank
        // depending on WebView2 version — always allow those + cid:/data: inline content.
        if (uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("cid:", StringComparison.OrdinalIgnoreCase) ||
            uri.StartsWith("blob:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // External http(s): block by default. Allow if the user has flipped the
        // global setting OR clicked the per-message Show Images banner.
        var allow = App.Settings.AllowRemoteImages || _allowImagesForThisMessage;
        if (!allow)
        {
            // Surface the banner so the user knows content was withheld. Marshal
            // to UI thread — WebResourceRequested fires off-thread.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (BlockedBanner.Visibility != Visibility.Visible)
                    BlockedBanner.Visibility = Visibility.Visible;
            }));

            // Reply with a 403 stub so the renderer doesn't hang waiting on us.
            e.Response = Web.CoreWebView2.Environment.CreateWebResourceResponse(
                Stream.Null, 403, "Blocked", "");
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // The first navigation per Render() is our own NavigateToString — let it through.
        if (!_initialNavigationConsumed)
        {
            _initialNavigationConsumed = true;
            return;
        }

        // Anything after that is a link click. Hand it off to the system browser.
        e.Cancel = true;
        var uri = e.Uri;
        if (string.IsNullOrEmpty(uri)) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
        }
        catch { /* ignore — user can copy it manually */ }
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        var uri = e.Uri;
        if (string.IsNullOrEmpty(uri)) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true });
        }
        catch { /* ignore */ }
    }

    private void ShowImagesOnce_Click(object sender, RoutedEventArgs e)
    {
        _allowImagesForThisMessage = true;
        BlockedBanner.Visibility = Visibility.Collapsed;
        Render();
    }

    // --------------------------------------------------------------------- //
    // HTML wrapping — give every body the same dark theme + sensible CSS reset.
    // --------------------------------------------------------------------- //

    private const string ThemeCss = @"
        :root { color-scheme: dark; }
        html, body { margin: 0; padding: 16px; }
        body {
            background: #1f1f1f;
            color: #f0f0f0;
            font: 14px/1.55 'Segoe UI Variable', 'Segoe UI', system-ui, sans-serif;
            word-wrap: break-word;
            overflow-wrap: break-word;
        }
        a { color: #5A8BFF; }
        img { max-width: 100%; height: auto; }
        table { max-width: 100%; }
        pre, code { white-space: pre-wrap; word-break: break-word; }
        blockquote {
            margin: 8px 0; padding: 6px 14px;
            border-left: 3px solid #3a3a3a;
            color: #c8c8c8;
        }
    ";

    private static string WrapHtmlBody(string innerHtml) => $@"<!doctype html>
<html><head><meta charset=""utf-8"">
<base target=""_blank"">
<style>{ThemeCss}</style>
</head><body>{innerHtml}</body></html>";

    private static string WrapPlainText(string text) =>
        WrapHtmlBody($"<pre style='font-family:inherit;font-size:inherit;'>{WebUtility.HtmlEncode(text ?? "")}</pre>");
}
