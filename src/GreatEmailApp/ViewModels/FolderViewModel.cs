// FILE: src/GreatEmailApp/ViewModels/FolderViewModel.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 2
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using GreatEmailApp.Core.Models;

namespace GreatEmailApp.ViewModels;

public partial class FolderViewModel : ObservableObject
{
    [ObservableProperty] private bool isSelected;
    [ObservableProperty] private bool isExpanded = true;

    public Folder Model { get; }
    public ObservableCollection<FolderViewModel> Children { get; }

    public string Id => Model.Id;
    public string Name => Model.Name;
    public int UnreadCount => Model.UnreadCount;
    public bool HasUnread => Model.UnreadCount > 0;
    public bool IsNested => Model.IsNested;
    public bool HasChildren => Children.Count > 0;

    /// <summary>Local-only draft count for the Drafts folder. Updated by
    /// MainViewModel on App.Drafts.Changed so the sidebar shows a badge.</summary>
    [ObservableProperty] private int localDraftCount;
    partial void OnLocalDraftCountChanged(int value)
    {
        OnPropertyChanged(nameof(BadgeCount));
        OnPropertyChanged(nameof(HasBadge));
    }

    /// <summary>Generalized badge: drafts folder uses LocalDraftCount, everything
    /// else falls back to IMAP UnreadCount. Sidebar template binds to this.</summary>
    public int BadgeCount => Model.Special == SpecialFolder.Drafts ? LocalDraftCount : UnreadCount;
    public bool HasBadge => BadgeCount > 0;

    /// <summary>Re-emit unread bindings so the sidebar chip refreshes.</summary>
    public void OnUnreadChanged()
    {
        OnPropertyChanged(nameof(UnreadCount));
        OnPropertyChanged(nameof(HasUnread));
        OnPropertyChanged(nameof(BadgeCount));
        OnPropertyChanged(nameof(HasBadge));
    }

    public string IconKey => Model.Special switch
    {
        SpecialFolder.Inbox => "IconInbox",
        SpecialFolder.Drafts => "IconDrafts",
        SpecialFolder.Outbox => "IconSent",
        SpecialFolder.Sent => "IconSent",
        SpecialFolder.Deleted => "IconTrash",
        SpecialFolder.Junk => "IconJunk",
        SpecialFolder.Archive => "IconArchive",
        _ => "IconFolder",
    };

    public FolderViewModel(Folder model)
    {
        Model = model;
        Children = new ObservableCollection<FolderViewModel>();
        // Sort children: alphabetical, special-folder buckets first.
        var sortedKids = model.Children
            .OrderBy(c => c.Special switch
            {
                SpecialFolder.Inbox => 0,
                SpecialFolder.Drafts => 1,
                SpecialFolder.Sent => 2,
                SpecialFolder.Archive => 3,
                SpecialFolder.Junk => 4,
                SpecialFolder.Deleted => 5,
                _ => 100,
            })
            .ThenBy(c => c.Name, System.StringComparer.OrdinalIgnoreCase);
        foreach (var child in sortedKids)
            Children.Add(new FolderViewModel(child));
    }
}
