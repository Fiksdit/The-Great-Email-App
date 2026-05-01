// FILE: src/GreatEmailApp/Controls/RichTextEditor.xaml.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Rich-text body authoring on a WebView2 contenteditable surface.
//
// Why WebView2 instead of WPF RichTextBox: outgoing message bodies need to
// be HTML, and WPF's FlowDocument → HTML conversion is rough. We already
// host WebView2 to RENDER incoming mail (MessageBodyView), so the editor
// shares the same engine — what you type is what you'd see arriving in
// the recipient's inbox.
//
// document.execCommand is technically deprecated, but it remains the most
// portable contenteditable formatting API and works fine in Edge/Chromium.
// The replacement (Selection/Range + custom commands) buys nothing for the
// formatting ops we expose.

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GreatEmailApp.Core.Storage;
using Microsoft.Web.WebView2.Core;

namespace GreatEmailApp.Controls;

public partial class RichTextEditor : UserControl
{
    private bool _coreReady;
    private string _pendingHtml = "";
    private bool _initialNavigationConsumed;

    public RichTextEditor()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>Fires after each change to the body content. Used by Send-can-execute checks.</summary>
    public event EventHandler? ContentChanged;

    // --------------------------------------------------------------------- //
    // Lifecycle
    // --------------------------------------------------------------------- //

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_coreReady) return;
        try
        {
            var userDataFolder = Path.Combine(AppPaths.Root, "WebView2Data");
            Directory.CreateDirectory(userDataFolder);
            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await Web.EnsureCoreWebView2Async(env);

            var s = Web.CoreWebView2.Settings;
            s.IsScriptEnabled = true;            // we own the script, it's our editor
            s.AreDefaultContextMenusEnabled = true;  // user expects spell-check menu etc.
            s.AreDevToolsEnabled = false;
            s.IsStatusBarEnabled = false;
            s.AreHostObjectsAllowed = false;

            // Outbound link clicks would be very confusing inside the editor —
            // the user is *typing* into a link, not following it.
            Web.CoreWebView2.NavigationStarting += (_, ev) =>
            {
                if (_initialNavigationConsumed) ev.Cancel = true;
                _initialNavigationConsumed = true;
            };
            Web.CoreWebView2.NewWindowRequested += (_, ev) =>
            {
                ev.Handled = true;
                if (!string.IsNullOrEmpty(ev.Uri))
                {
                    try { Process.Start(new ProcessStartInfo { FileName = ev.Uri, UseShellExecute = true }); }
                    catch { }
                }
            };

            // The page raises a postMessage on every input event — we forward it
            // out as ContentChanged so consumers can re-evaluate Send.CanExecute.
            Web.CoreWebView2.WebMessageReceived += (_, _) => ContentChanged?.Invoke(this, EventArgs.Empty);

            _coreReady = true;
            LoadHtml(_pendingHtml);
        }
        catch (Exception ex)
        {
            // WebView2 missing — degrade gracefully. ComposeWindow can fall back
            // to plain text by checking IsRuntimeAvailable.
            Web.Visibility = Visibility.Collapsed;
            IsRuntimeAvailable = false;
            FailureReason = ex.Message;
        }
    }

    public bool IsRuntimeAvailable { get; private set; } = true;
    public string? FailureReason { get; private set; }

    // --------------------------------------------------------------------- //
    // Public API
    // --------------------------------------------------------------------- //

    /// <summary>Replace the editor contents with the given HTML fragment.</summary>
    public void SetHtml(string html)
    {
        _pendingHtml = html ?? "";
        if (_coreReady) LoadHtml(_pendingHtml);
    }

    /// <summary>Read the current editor contents back as HTML.</summary>
    public async Task<string> GetHtmlAsync()
    {
        if (!_coreReady) return _pendingHtml;
        // ExecuteScriptAsync returns a JSON-serialized result.
        var json = await Web.CoreWebView2.ExecuteScriptAsync("document.getElementById('editor').innerHTML");
        try { return JsonSerializer.Deserialize<string>(json) ?? ""; }
        catch { return ""; }
    }

    /// <summary>Read the current editor contents as plain text (for the text/plain MIME alternative).</summary>
    public async Task<string> GetPlainTextAsync()
    {
        if (!_coreReady) return _pendingHtml;
        var json = await Web.CoreWebView2.ExecuteScriptAsync("document.getElementById('editor').innerText");
        try { return JsonSerializer.Deserialize<string>(json) ?? ""; }
        catch { return ""; }
    }

    // --------------------------------------------------------------------- //
    // Toolbar wiring
    // --------------------------------------------------------------------- //

    private async void Exec(string cmd, string? arg = null)
    {
        if (!_coreReady) return;
        var argLiteral = arg is null ? "null" : "'" + arg.Replace("'", "\\'") + "'";
        await Web.CoreWebView2.ExecuteScriptAsync(
            $"document.execCommand('{cmd}', false, {argLiteral}); window.chrome.webview.postMessage('changed');");
    }

    private void OnBold(object s, RoutedEventArgs e)        => Exec("bold");
    private void OnItalic(object s, RoutedEventArgs e)      => Exec("italic");
    private void OnUnderline(object s, RoutedEventArgs e)   => Exec("underline");
    private void OnBullets(object s, RoutedEventArgs e)     => Exec("insertUnorderedList");
    private void OnNumbers(object s, RoutedEventArgs e)     => Exec("insertOrderedList");
    private void OnQuote(object s, RoutedEventArgs e)       => Exec("formatBlock", "blockquote");
    private void OnClearFormat(object s, RoutedEventArgs e) => Exec("removeFormat");
    private void OnLink(object sender, RoutedEventArgs e)
    {
        var url = LinkPrompt.AskForUrl(Window.GetWindow(this));
        if (string.IsNullOrEmpty(url)) return;
        Exec("createLink", url);
    }

    // --------------------------------------------------------------------- //
    // Page bootstrap
    // --------------------------------------------------------------------- //

    private void LoadHtml(string innerHtml)
    {
        _initialNavigationConsumed = false;
        Web.NavigateToString(BuildPage(innerHtml));
    }

    /// <summary>
    /// Editor host page. The body is a single contenteditable div; we forward
    /// every input event back to .NET via window.chrome.webview.postMessage
    /// so the host can drive Send.CanExecute. Theme matches the rest of the app.
    /// </summary>
    private static string BuildPage(string innerHtml) => $@"<!doctype html>
<html><head><meta charset=""utf-8"">
<style>
  :root {{ color-scheme: dark; }}
  html, body {{ height: 100%; margin: 0; }}
  body {{
    background: #1f1f1f;
    color: #f0f0f0;
    font: 14px/1.55 'Segoe UI Variable', 'Segoe UI', system-ui, sans-serif;
  }}
  #editor {{
    min-height: 100%; padding: 14px; outline: none;
    word-wrap: break-word; overflow-wrap: break-word;
  }}
  #editor:focus {{ outline: none; }}
  a {{ color: #5A8BFF; }}
  blockquote {{ margin: 8px 0; padding: 6px 14px; border-left: 3px solid #3a3a3a; color: #c8c8c8; }}
  ul, ol {{ margin: 8px 0; padding-left: 24px; }}
</style>
</head>
<body>
  <div id=""editor"" contenteditable=""true"" spellcheck=""true"">{innerHtml}</div>
  <script>
    var ed = document.getElementById('editor');
    function notify() {{ try {{ window.chrome.webview.postMessage('changed'); }} catch(e) {{}} }}
    ed.addEventListener('input', notify);
    ed.focus();
  </script>
</body></html>";

    /// <summary>Tiny modal "enter URL" prompt. WPF doesn't have a builtin, so we roll one.</summary>
    private static class LinkPrompt
    {
        public static string? AskForUrl(Window? owner)
        {
            var win = new Window
            {
                Owner = owner,
                Title = "Insert link",
                Width = 420, Height = 140,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = (System.Windows.Media.Brush)Application.Current.Resources["AppBackgroundBrush"],
            };
            var panel = new StackPanel { Margin = new Thickness(16) };
            panel.Children.Add(new TextBlock
            {
                Text = "URL:",
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["SecondaryTextBrush"],
                Margin = new Thickness(0, 0, 0, 6),
            });
            var box = new TextBox { Padding = new Thickness(6, 4, 6, 4), Text = "https://" };
            panel.Children.Add(box);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal,
                                            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            var ok = new Button { Content = "Insert", Padding = new Thickness(14, 6, 14, 6), IsDefault = true };
            var cancel = new Button { Content = "Cancel", Padding = new Thickness(14, 6, 14, 6),
                                       Margin = new Thickness(8, 0, 0, 0), IsCancel = true };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            panel.Children.Add(buttons);
            win.Content = panel;
            string? result = null;
            ok.Click += (_, _) => { result = box.Text; win.DialogResult = true; };
            box.SelectAll();
            box.Focus();
            return win.ShowDialog() == true ? result : null;
        }
    }
}
