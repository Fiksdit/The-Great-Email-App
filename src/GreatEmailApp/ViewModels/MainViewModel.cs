// FILE: src/GreatEmailApp/ViewModels/MainViewModel.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 2
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Sample;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IImapService _imap;
    private readonly ICredentialStore _creds;
    private readonly IAccountStore _accountStore;

    public ObservableCollection<AccountViewModel> Accounts { get; } = new();
    public ObservableCollection<MessageViewModel> Messages { get; } = new();

    [ObservableProperty] private string activeRibbonTab = "Home";
    [ObservableProperty] private FolderViewModel? selectedFolder;
    [ObservableProperty] private MessageViewModel? selectedMessage;
    [ObservableProperty] private string filter = "All";
    [ObservableProperty] private int zoom = 100;
    [ObservableProperty] private string searchText = "";
    [ObservableProperty] private string statusMessage = "Ready";
    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private bool hasAccounts;

    public string AppTitle => "The Great Email App";
    public string AccountInitial => "JR";
    public string AccountEmail => "coolman0804@outlook.com";

    private CancellationTokenSource? _messageLoadCts;
    private CancellationTokenSource? _bodyLoadCts;

    public MainViewModel() : this(App.Imap, App.Credentials, App.Accounts) { }

    public MainViewModel(IImapService imap, ICredentialStore creds, IAccountStore accountStore)
    {
        _imap = imap;
        _creds = creds;
        _accountStore = accountStore;
        LoadAccounts();
    }

    private void LoadAccounts()
    {
        Accounts.Clear();
        var stored = _accountStore.LoadAll();

        if (stored.Count == 0)
        {
            // First-run / empty state: show the design's sample data so the UI
            // isn't blank. As soon as the user adds a real account, sample data
            // is wiped and replaced. NOTE: sample data is read-only — clicks
            // don't try to fetch from a server.
            HasAccounts = false;
            foreach (var a in SampleData.GetAccounts())
                Accounts.Add(new AccountViewModel(a));
            LoadSampleMessages();
            StatusMessage = "Demo data — click \"Add account\" to connect a real IMAP account.";
            return;
        }

        HasAccounts = true;
        foreach (var a in stored)
        {
            var vm = new AccountViewModel(a);
            Accounts.Add(vm);
            // Kick off folder load. Fire-and-forget; UI updates on completion.
            _ = LoadFoldersAsync(vm);
        }
        Messages.Clear();
        StatusMessage = $"Loaded {stored.Count} account(s).";
    }

    private void LoadSampleMessages()
    {
        Messages.Clear();
        foreach (var m in SampleData.GetMessages())
            Messages.Add(new MessageViewModel(m));
        MarkGroupTransitions();

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

    private async Task LoadFoldersAsync(AccountViewModel accountVm)
    {
        var account = accountVm.Model;
        var creds = _creds.Read(account.Id);
        if (creds is null)
        {
            account.Status = AccountStatus.Error;
            StatusMessage = $"No password stored for {account.EmailAddress}. Re-add the account.";
            return;
        }

        account.Status = AccountStatus.Syncing;

        var result = await _imap.ListFoldersAsync(account, creds.Value.Password);
        if (result is Result<System.Collections.Generic.List<Folder>>.Ok ok)
        {
            account.Status = AccountStatus.Connected;
            // Replace folder collection
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                accountVm.Folders.Clear();

                // ok.Value contains the top-level folders; each folder's Children
                // collection holds its sub-tree (FolderViewModel ctor recurses).

                // Inject synthetic Outbox — IMAP doesn't have one, but Outlook UX
                // expects it. FullPath="" tells SelectFolderAsync to skip IMAP fetch.
                var roots = ok.Value.ToList();
                roots.Add(new Folder
                {
                    Id = $"{account.Id}:outbox",
                    Name = "Outbox",
                    AccountId = account.Id,
                    FullPath = "",
                    Special = SpecialFolder.Outbox,
                });

                // Outlook-style ordering at the root: special folders in fixed order,
                // everything else alphabetical.
                var sorted = roots
                    .OrderBy(f => f.Special switch
                    {
                        SpecialFolder.Inbox   => 0,
                        SpecialFolder.Drafts  => 1,
                        SpecialFolder.Outbox  => 2,
                        SpecialFolder.Sent    => 3,
                        SpecialFolder.Archive => 4,
                        SpecialFolder.Junk    => 5,
                        SpecialFolder.Deleted => 6,
                        _                     => 100,
                    })
                    .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var f in sorted)
                {
                    f.AccountId = account.Id;
                    accountVm.Folders.Add(new FolderViewModel(f));
                }
                StatusMessage = $"{account.EmailAddress}: {ok.Value.Count} folders.";

                // Auto-select inbox if nothing's selected
                if (SelectedFolder is null)
                {
                    var inbox = accountVm.Folders.FirstOrDefault(x => x.Model.Special == SpecialFolder.Inbox)
                              ?? accountVm.Folders.FirstOrDefault();
                    if (inbox is not null)
                    {
                        SelectFolderCommand.Execute(inbox);
                    }
                }
            });
        }
        else if (result is Result<System.Collections.Generic.List<Folder>>.Fail f)
        {
            account.Status = AccountStatus.Error;
            StatusMessage = $"{account.EmailAddress}: {f.Error}";
        }
    }

    /// <summary>Reload accounts from the store. Called after the Settings dialog
    /// adds/removes accounts so the sidebar mirrors disk state.</summary>
    public void ReloadAccounts()
    {
        LoadAccounts();
    }

    /// <summary>Called by AddAccountDialog after a new account has been persisted.</summary>
    public void OnAccountAdded(Account account)
    {
        if (!HasAccounts)
        {
            // First real account — wipe sample data
            Accounts.Clear();
            Messages.Clear();
            HasAccounts = true;
        }
        var vm = new AccountViewModel(account);
        Accounts.Add(vm);
        _ = LoadFoldersAsync(vm);
    }

    [RelayCommand]
    private async Task SelectFolderAsync(FolderViewModel? folder)
    {
        if (folder is null) return;
        if (SelectedFolder is not null) SelectedFolder.IsSelected = false;
        folder.IsSelected = true;
        SelectedFolder = folder;

        // Demo mode: don't hit a server
        if (!HasAccounts) return;

        // Synthetic folders (Outbox) — no IMAP path, just show empty.
        if (string.IsNullOrEmpty(folder.Model.FullPath))
        {
            Messages.Clear();
            SelectedMessage = null;
            StatusMessage = $"{folder.Name}: empty.";
            return;
        }

        var account = Accounts.FirstOrDefault(a => a.Model.Id == folder.Model.AccountId)?.Model;
        if (account is null) return;
        var creds = _creds.Read(account.Id);
        if (creds is null) return;

        _messageLoadCts?.Cancel();
        _messageLoadCts = new CancellationTokenSource();
        var ct = _messageLoadCts.Token;

        IsBusy = true;
        StatusMessage = $"Loading {folder.Name}…";
        Messages.Clear();
        SelectedMessage = null;

        var res = await _imap.ListMessagesAsync(account, creds.Value.Password, folder.Model.FullPath, 200, ct);
        if (ct.IsCancellationRequested) return;

        if (res is Result<System.Collections.Generic.List<Message>>.Ok ok)
        {
            foreach (var m in ok.Value)
                Messages.Add(new MessageViewModel(m));
            MarkGroupTransitions();
            StatusMessage = $"{folder.Name}: {ok.Value.Count} messages.";

            var first = Messages.FirstOrDefault();
            if (first is not null)
                await SelectMessageAsync(first);
        }
        else if (res is Result<System.Collections.Generic.List<Message>>.Fail f)
        {
            StatusMessage = $"Failed to load {folder.Name}: {f.Error}";
        }
        IsBusy = false;
    }

    [RelayCommand]
    private async Task SelectMessageAsync(MessageViewModel? message)
    {
        if (message is null) return;
        if (SelectedMessage is not null) SelectedMessage.IsSelected = false;
        message.IsSelected = true;
        SelectedMessage = message;

        if (!HasAccounts) return;
        if (string.IsNullOrEmpty(message.Model.AccountId)) return;

        var account = Accounts.FirstOrDefault(a => a.Model.Id == message.Model.AccountId)?.Model;
        if (account is null) return;
        var creds = _creds.Read(account.Id);
        if (creds is null) return;

        if (!uint.TryParse(message.Model.Id, out var uid)) return;

        _bodyLoadCts?.Cancel();
        _bodyLoadCts = new CancellationTokenSource();
        var ct = _bodyLoadCts.Token;

        var res = await _imap.FetchBodyAsync(account, creds.Value.Password, message.Model.FolderId, uid, ct);
        if (ct.IsCancellationRequested) return;

        if (res is Result<(string PlainText, string Html)>.Ok ok)
        {
            message.Model.BodyPlain = ok.Value.PlainText;
            message.Model.BodyHtml = ok.Value.Html;
            // Force a re-bind via OnPropertyChanged
            message.OnBodyLoaded();
            // SelectedMessage didn't change, but the body did — re-emit to refresh reading pane bindings.
            OnPropertyChanged(nameof(SelectedMessage));
        }
    }

    [RelayCommand]
    private void SetRibbonTab(string tab) => ActiveRibbonTab = tab;

    [RelayCommand]
    private void SetFilter(string f) => Filter = f;

    private void MarkGroupTransitions()
    {
        string? prev = null;
        foreach (var msg in Messages)
        {
            msg.IsFirstInGroup = msg.Group != prev;
            prev = msg.Group;
        }
    }
}
