// FILE: src/GreatEmailApp/ViewModels/AccountViewModel.cs
// Created: 2026-04-29 | Revised: 2026-11-21 | Rev: 2
// Changed by: Claude Opus 5.5 on behalf of James Reed

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using GreatEmailApp.Core.Models;

namespace GreatEmailApp.ViewModels;

public partial class AccountViewModel : ObservableObject
{
    [ObservableProperty] private bool isExpanded = true;

    // True when this PC has no IMAP password stored for the account (e.g. a new
    // PC that pulled the account list from cloud sync — Credential Manager
    // doesn't roam). The sidebar shows an "Enter password" prompt under the row.
    [ObservableProperty] private bool needsPassword;

    public Account Model { get; }
    public ObservableCollection<FolderViewModel> Folders { get; }

    public string DisplayName => Model.DisplayName;
    public string EmailAddress => Model.EmailAddress;
    public string Initials => Model.Initials;
    public AccountStatus Status => Model.Status;

    public Brush ColorBrush { get; }

    // Computed on read (was computed once in the ctor, so the dot stayed grey
    // forever — Model.Status changes never reached the UI).
    public Brush StatusBrush => Model.Status switch
    {
        AccountStatus.Connected => ConnectedBrush,
        AccountStatus.Syncing   => SyncingBrush,
        AccountStatus.Error     => ErrorBrush,
        _                       => OfflineBrush,
    };

    private static readonly SolidColorBrush ConnectedBrush = HexBrush("#14A37F");
    private static readonly SolidColorBrush SyncingBrush   = HexBrush("#D29014");
    private static readonly SolidColorBrush ErrorBrush     = HexBrush("#D4406B");
    private static readonly SolidColorBrush OfflineBrush   = HexBrush("#888888");

    public AccountViewModel(Account model)
    {
        Model = model;
        Folders = new ObservableCollection<FolderViewModel>();
        foreach (var f in model.Folders)
            Folders.Add(new FolderViewModel(f));

        ColorBrush = HexBrush(model.Color);
    }

    /// <summary>Set the account's connection status and repaint the status dot.</summary>
    public void SetStatus(AccountStatus status)
    {
        Model.Status = status;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(StatusBrush));
    }

    private static SolidColorBrush HexBrush(string hex)
    {
        var s = hex.TrimStart('#');
        if (s.Length == 3) s = string.Concat(s[0], s[0], s[1], s[1], s[2], s[2]);
        var c = Color.FromRgb(
            System.Convert.ToByte(s.Substring(0, 2), 16),
            System.Convert.ToByte(s.Substring(2, 2), 16),
            System.Convert.ToByte(s.Substring(4, 2), 16));
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }
}
