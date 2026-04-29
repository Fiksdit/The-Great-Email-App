// FILE: src/GreatEmailApp/ViewModels/MainViewModel.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Sample;

namespace GreatEmailApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ObservableCollection<AccountViewModel> Accounts { get; }
    public ObservableCollection<MessageViewModel> Messages { get; }

    [ObservableProperty] private string activeRibbonTab = "Home";
    [ObservableProperty] private FolderViewModel? selectedFolder;
    [ObservableProperty] private MessageViewModel? selectedMessage;
    [ObservableProperty] private string filter = "All";
    [ObservableProperty] private int zoom = 100;
    [ObservableProperty] private string searchText = "";

    // Window header / status
    public string AppTitle => "The Great Email App";
    public string AccountInitial => "JR";
    public string AccountEmail => "coolman0804@outlook.com";
    public bool SyncOn { get; set; } = true;

    public MainViewModel()
    {
        Accounts = new ObservableCollection<AccountViewModel>(
            SampleData.GetAccounts().Select(a => new AccountViewModel(a)));

        Messages = new ObservableCollection<MessageViewModel>(
            SampleData.GetMessages().Select(m => new MessageViewModel(m)));

        // Mark group transitions so the list shows a "Today / Yesterday / Last Week" caption.
        string? prevGroup = null;
        foreach (var msg in Messages)
        {
            msg.IsFirstInGroup = msg.Group != prevGroup;
            prevGroup = msg.Group;
        }

        // Default selection: first account's Inbox + first email.
        var firstInbox = Accounts.FirstOrDefault()?.Folders
            .FirstOrDefault(f => f.Model.Special == SpecialFolder.Inbox);
        if (firstInbox is not null)
        {
            firstInbox.IsSelected = true;
            SelectedFolder = firstInbox;
        }
        var firstMsg = Messages.FirstOrDefault();
        if (firstMsg is not null)
        {
            firstMsg.IsSelected = true;
            SelectedMessage = firstMsg;
        }
    }

    [RelayCommand]
    private void SelectFolder(FolderViewModel? folder)
    {
        if (folder is null) return;
        if (SelectedFolder is not null) SelectedFolder.IsSelected = false;
        folder.IsSelected = true;
        SelectedFolder = folder;
    }

    [RelayCommand]
    private void SelectMessage(MessageViewModel? message)
    {
        if (message is null) return;
        if (SelectedMessage is not null) SelectedMessage.IsSelected = false;
        message.IsSelected = true;
        SelectedMessage = message;
    }

    [RelayCommand]
    private void SetRibbonTab(string tab)
    {
        ActiveRibbonTab = tab;
    }

    [RelayCommand]
    private void SetFilter(string f) => Filter = f;
}
