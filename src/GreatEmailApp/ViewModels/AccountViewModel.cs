// FILE: src/GreatEmailApp/ViewModels/AccountViewModel.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using GreatEmailApp.Core.Models;

namespace GreatEmailApp.ViewModels;

public partial class AccountViewModel : ObservableObject
{
    [ObservableProperty] private bool isExpanded = true;

    public Account Model { get; }
    public ObservableCollection<FolderViewModel> Folders { get; }

    public string DisplayName => Model.DisplayName;
    public string EmailAddress => Model.EmailAddress;
    public string Initials => Model.Initials;
    public AccountStatus Status => Model.Status;

    public Brush ColorBrush { get; }
    public Brush StatusBrush { get; }

    public AccountViewModel(Account model)
    {
        Model = model;
        Folders = new ObservableCollection<FolderViewModel>();
        foreach (var f in model.Folders)
            Folders.Add(new FolderViewModel(f));

        ColorBrush = HexBrush(model.Color);
        StatusBrush = model.Status switch
        {
            AccountStatus.Connected => HexBrush("#14A37F"),
            AccountStatus.Syncing => HexBrush("#D29014"),
            AccountStatus.Error => HexBrush("#D4406B"),
            _ => HexBrush("#888888"),
        };
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
