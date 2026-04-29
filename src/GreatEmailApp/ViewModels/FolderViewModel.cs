// FILE: src/GreatEmailApp/ViewModels/FolderViewModel.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using CommunityToolkit.Mvvm.ComponentModel;
using GreatEmailApp.Core.Models;

namespace GreatEmailApp.ViewModels;

public partial class FolderViewModel : ObservableObject
{
    [ObservableProperty] private bool isSelected;

    public Folder Model { get; }

    public string Id => Model.Id;
    public string Name => Model.Name;
    public int UnreadCount => Model.UnreadCount;
    public bool HasUnread => Model.UnreadCount > 0;
    public bool IsNested => Model.IsNested;

    public string IconKey => Model.Special switch
    {
        SpecialFolder.Inbox => "IconInbox",
        SpecialFolder.Drafts => "IconDrafts",
        SpecialFolder.Sent => "IconSent",
        SpecialFolder.Deleted => "IconTrash",
        SpecialFolder.Junk => "IconJunk",
        SpecialFolder.Archive => "IconArchive",
        _ => "IconFolder",
    };

    public FolderViewModel(Folder model)
    {
        Model = model;
    }
}
