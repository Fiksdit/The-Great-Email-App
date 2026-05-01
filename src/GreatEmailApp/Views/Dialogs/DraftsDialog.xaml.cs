// FILE: src/GreatEmailApp/Views/Dialogs/DraftsDialog.xaml.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Views.Dialogs;

public partial class DraftsDialog : Window
{
    public DraftsDialog()
    {
        InitializeComponent();
        Refresh();
    }

    private void Refresh()
    {
        var drafts = App.Drafts.LoadAll().OrderByDescending(d => d.UpdatedAt).ToList();
        List.ItemsSource = drafts;
        EmptyHint.Visibility = drafts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Open_Click(object sender, RoutedEventArgs e) => OpenSelected();
    private void List_DoubleClick(object sender, MouseButtonEventArgs e) => OpenSelected();

    private void OpenSelected()
    {
        if (List.SelectedItem is not Draft d) return;
        var win = ComposeWindow.OpenDraft(App.Accounts.LoadAll(), d);
        win.Owner = Owner;
        win.Show();
        Close();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string id)
        {
            App.Drafts.Delete(id);
            Refresh();
        }
    }
}
