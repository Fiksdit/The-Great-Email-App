// FILE: src/GreatEmailApp/Views/Dialogs/FolderNameDialog.xaml.cs
// Created: 2026-05-12 | Revised: 2026-05-12 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Windows;

namespace GreatEmailApp.Views.Dialogs;

/// <summary>
/// Text-prompt dialog used by folder Create / Rename. Caller sets Title /
/// Help / InitialName via the constructor, then reads <see cref="Name"/>
/// after <see cref="Window.ShowDialog"/> returns true.
/// </summary>
public partial class FolderNameDialog : Window
{
    /// <summary>The trimmed name the user accepted. Null if the dialog was cancelled.</summary>
    public string? Name { get; private set; }

    public FolderNameDialog(string titleText, string helpText, string initialName = "")
    {
        InitializeComponent();
        TitleText.Text = titleText;
        HelpText.Text = helpText;
        NameBox.Text = initialName;
        Loaded += (_, _) =>
        {
            NameBox.Focus();
            NameBox.SelectAll();
        };
    }

    private void OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        ErrorText.Text = "";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var n = NameBox.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(n))
        {
            ErrorText.Text = "Name is required.";
            return;
        }

        // IMAP folder name validation per RFC 3501 §5.1: '/' is the conventional
        // hierarchy delimiter, so we forbid it in a single segment to avoid users
        // accidentally creating implicit subfolder paths. Most servers also reject
        // these characters outright.
        foreach (var bad in new[] { '/', '\\', '"', '\0' })
        {
            if (n.Contains(bad))
            {
                ErrorText.Text = $"Folder name can't contain '{bad}'.";
                return;
            }
        }

        Name = n;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Name = null;
        DialogResult = false;
        Close();
    }
}
