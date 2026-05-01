// FILE: src/GreatEmailApp/Controls/AddressInput.xaml.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Address-list textbox with contact autocomplete.
//
// We treat the textbox as a comma-separated list. The "current token" is the
// substring between the last comma (or start of text) and the caret. As the
// user types, we re-query the suggester with that token; arrow keys / Enter
// pick a suggestion and replace just that token, leaving any earlier
// recipients alone.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Controls;

public partial class AddressInput : UserControl
{
    public AddressInput()
    {
        InitializeComponent();
    }

    // --- Bindable Text (two-way) ---------------------------------------- //

    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(AddressInput),
            new FrameworkPropertyMetadata("",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnTextChangedExternal));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private bool _suppressExternalSync;
    private static void OnTextChangedExternal(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ai = (AddressInput)d;
        if (ai._suppressExternalSync) return;
        if (ai.Box.Text != (string)e.NewValue) ai.Box.Text = (string)e.NewValue ?? "";
    }

    // --- Bindable suggestion provider ----------------------------------- //

    /// <summary>Function that returns up to N suggestions for a partial token.
    /// Set by the host (ComposeWindow) right after construction.</summary>
    public Func<string, IReadOnlyList<Contact>>? Suggester { get; set; }

    // --- Box events ----------------------------------------------------- //

    private void Box_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Push back to the dependency property so bindings see live changes.
        _suppressExternalSync = true;
        try { Text = Box.Text; }
        finally { _suppressExternalSync = false; }

        ShowSuggestionsForCaret();
    }

    private void Box_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!Suggestions.IsOpen) return;

        if (e.Key == Key.Down)
        {
            FocusList(0);
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            FocusList(List.Items.Count - 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && List.Items.Count > 0)
        {
            List.SelectedIndex = 0;
            AcceptSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Suggestions.IsOpen = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Tab && List.Items.Count > 0)
        {
            List.SelectedIndex = 0;
            AcceptSelected();
            e.Handled = true;
        }
    }

    private void Box_LostFocus(object sender, RoutedEventArgs e)
    {
        // Delay so a click into the popup doesn't immediately close it.
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!Box.IsKeyboardFocusWithin && !List.IsKeyboardFocusWithin)
                Suggestions.IsOpen = false;
        }));
    }

    private void List_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AcceptSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            Suggestions.IsOpen = false;
            Box.Focus();
            e.Handled = true;
        }
    }

    private void List_MouseLeftButtonUp(object sender, MouseButtonEventArgs e) => AcceptSelected();

    // --- Suggestion logic ----------------------------------------------- //

    private void ShowSuggestionsForCaret()
    {
        var token = CurrentToken();
        if (Suggester is null || string.IsNullOrWhiteSpace(token))
        {
            Suggestions.IsOpen = false;
            return;
        }

        // Don't suggest if the token already looks like a complete email.
        if (token.Contains('@') && !token.EndsWith('.'))
        {
            // Allow suggestions to keep flowing for "foo@" but stop once it looks
            // like a finished address (contains @ and a tld char after).
            var afterAt = token[(token.IndexOf('@') + 1)..];
            if (afterAt.Contains('.') && afterAt.Length > token.IndexOf('@') + 2)
            {
                Suggestions.IsOpen = false;
                return;
            }
        }

        var hits = Suggester(token);
        if (hits.Count == 0)
        {
            Suggestions.IsOpen = false;
            return;
        }
        List.ItemsSource = hits;
        List.SelectedIndex = 0;
        Suggestions.IsOpen = true;
    }

    private void FocusList(int index)
    {
        List.Focus();
        if (List.Items.Count == 0) return;
        index = Math.Max(0, Math.Min(index, List.Items.Count - 1));
        List.SelectedIndex = index;
        if (List.ItemContainerGenerator.ContainerFromIndex(index) is ListBoxItem item)
            item.Focus();
    }

    private void AcceptSelected()
    {
        if (List.SelectedItem is not Contact c) return;
        var addr = string.IsNullOrWhiteSpace(c.DisplayName)
            ? c.EmailAddress
            : $"{c.DisplayName} <{c.EmailAddress}>";
        ReplaceCurrentTokenWith(addr);
        Suggestions.IsOpen = false;
        Box.Focus();
    }

    /// <summary>Returns the substring between the last comma (or start) and the caret.</summary>
    private string CurrentToken()
    {
        var caret = Box.CaretIndex;
        var text = Box.Text ?? "";
        var head = text[..Math.Min(caret, text.Length)];
        var lastSep = head.LastIndexOfAny(new[] { ',', ';' });
        var tokenStart = lastSep < 0 ? 0 : lastSep + 1;
        return head[tokenStart..].TrimStart();
    }

    /// <summary>Replace the current token (caret-anchored) with <paramref name="addr"/> + ", ".</summary>
    private void ReplaceCurrentTokenWith(string addr)
    {
        var text = Box.Text ?? "";
        var caret = Box.CaretIndex;
        var head = text[..Math.Min(caret, text.Length)];
        var tail = text[Math.Min(caret, text.Length)..];

        var lastSep = head.LastIndexOfAny(new[] { ',', ';' });
        var tokenStart = lastSep < 0 ? 0 : lastSep + 1;

        // Preserve any leading whitespace from after the comma.
        var leading = "";
        var i = tokenStart;
        while (i < head.Length && char.IsWhiteSpace(head[i])) { leading += head[i]; i++; }

        var newText = text[..tokenStart] + leading + addr + ", " + tail.TrimStart();
        _suppressExternalSync = true;
        try
        {
            Box.Text = newText;
            Text = newText;
        }
        finally { _suppressExternalSync = false; }
        Box.CaretIndex = (text[..tokenStart] + leading + addr + ", ").Length;
    }
}
