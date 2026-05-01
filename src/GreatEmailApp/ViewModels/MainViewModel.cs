// FILE: src/GreatEmailApp/ViewModels/MainViewModel.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 2
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreatEmailApp.Core.Models;
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
    private DispatcherTimer? _markReadTimer;
    private MessageViewModel? _markReadPending;

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
        Messages.Clear();
        var stored = _accountStore.LoadAll();

        if (stored.Count == 0)
        {
            // Empty state: real, not faked. The sidebar shows the welcome
            // template; clicking "Add account" opens AddAccountDialog.
            HasAccounts = false;
            StatusMessage = "No accounts yet — click \"Add account\" to get started.";
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
        StatusMessage = $"Loaded {stored.Count} account(s).";
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

    /// <summary>
    /// Deep-link target for a search result click — selects the account, loads
    /// the folder if needed, and selects the message by uid.
    /// </summary>
    public async Task NavigateToMessageAsync(string accountId, string folderPath, uint uid)
    {
        var accountVm = Accounts.FirstOrDefault(a => a.Model.Id == accountId);
        if (accountVm is null) return;

        var folderVm = FindFolder(accountVm.Folders, folderPath);
        if (folderVm is null) return;

        // Loading the folder also re-selects, so we want to land on a specific
        // message after it loads. Wait for the load, then look for the uid.
        await SelectFolderAsync(folderVm);
        var hit = Messages.FirstOrDefault(m => m.Model.Id == uid.ToString());
        if (hit is not null) await SelectMessageAsync(hit);
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

            // Index for search — fire and forget, never block the UI on disk I/O.
            _ = App.MessageCache.UpsertEnvelopesAsync(account.Id, account.EmailAddress,
                folder.Model.FullPath, ok.Value);

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

        // Cancel any pending mark-as-read for the previously selected message,
        // then arm a new timer for THIS one.
        CancelPendingMarkRead();

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
            message.OnBodyLoaded();
            OnPropertyChanged(nameof(SelectedMessage));

            // Persist body text to the search cache so future searches can find on
            // body content (the poll-based indexer only writes envelopes).
            _ = App.MessageCache.UpsertBodyAsync(account.Id, message.Model.FolderId, uid,
                ok.Value.PlainText ?? "");
        }

        // Auto-mark-as-read after the configured delay (settings.MarkReadDelaySeconds).
        // -1 = never; 0 = immediately; otherwise wait N seconds and confirm we're
        // still on this same message before marking.
        ArmMarkReadTimer(message);
    }

    private void ArmMarkReadTimer(MessageViewModel message)
    {
        if (!message.Unread) return;
        var delay = App.Settings.MarkReadDelaySeconds;
        if (delay < 0) return;
        if (delay == 0) { _ = MarkAsReadAsync(message); return; }

        _markReadPending = message;
        _markReadTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
        _markReadTimer.Tick += async (_, _) =>
        {
            _markReadTimer?.Stop();
            _markReadTimer = null;
            // Only mark if the user is still looking at this message.
            if (_markReadPending == message && SelectedMessage == message)
            {
                await MarkAsReadAsync(message);
            }
            _markReadPending = null;
        };
        _markReadTimer.Start();
    }

    private void CancelPendingMarkRead()
    {
        _markReadTimer?.Stop();
        _markReadTimer = null;
        _markReadPending = null;
    }

    private async Task MarkAsReadAsync(MessageViewModel message)
    {
        if (!message.Unread) return;
        await SetReadStateAsync(message, seen: true, silent: true);
    }

    private async Task SetReadStateAsync(MessageViewModel message, bool seen, bool silent)
    {
        var account = Accounts.FirstOrDefault(a => a.Model.Id == message.Model.AccountId)?.Model;
        if (account is null) return;
        var creds = _creds.Read(account.Id);
        if (creds is null) return;
        if (!uint.TryParse(message.Model.Id, out var uid)) return;

        var res = await _imap.SetSeenAsync(account, creds.Value.Password, message.Model.FolderId, uid, seen);
        if (res.IsOk)
        {
            message.Model.Unread = !seen;
            message.OnReadStateChanged();
            // Update the folder's unread count in the sidebar
            UpdateFolderUnreadCount(message.Model.FolderId, seen ? -1 : +1);
        }
        else if (!silent)
        {
            StatusMessage = $"Failed: {res.AsError}";
        }
    }

    private void UpdateFolderUnreadCount(string folderPath, int delta)
    {
        foreach (var acc in Accounts)
        {
            var match = FindFolder(acc.Folders, folderPath);
            if (match is not null)
            {
                match.Model.UnreadCount = Math.Max(0, match.Model.UnreadCount + delta);
                match.OnUnreadChanged();
                return;
            }
        }
    }

    private static FolderViewModel? FindFolder(IEnumerable<FolderViewModel> folders, string path)
    {
        foreach (var f in folders)
        {
            if (f.Model.FullPath == path) return f;
            var inner = FindFolder(f.Children, path);
            if (inner is not null) return inner;
        }
        return null;
    }

    [RelayCommand]
    private async Task ToggleReadAsync(MessageViewModel? message)
    {
        var m = message ?? SelectedMessage;
        if (m is null) return;
        await SetReadStateAsync(m, seen: m.Unread, silent: false);
    }

    [RelayCommand]
    private async Task ToggleFlagAsync(MessageViewModel? message)
    {
        var m = message ?? SelectedMessage;
        if (m is null) return;
        var account = Accounts.FirstOrDefault(a => a.Model.Id == m.Model.AccountId)?.Model;
        if (account is null) return;
        var creds = _creds.Read(account.Id);
        if (creds is null) return;
        if (!uint.TryParse(m.Model.Id, out var uid)) return;

        var newFlagged = !m.Flagged;
        var res = await _imap.SetFlaggedAsync(account, creds.Value.Password, m.Model.FolderId, uid, newFlagged);
        if (res.IsOk)
        {
            m.Model.Flagged = newFlagged;
            m.OnFlagStateChanged();
        }
        else
        {
            StatusMessage = $"Failed: {res.AsError}";
        }
    }

    [RelayCommand]
    private Task ArchiveAsync(MessageViewModel? message)
        => MoveToSpecialAsync(message ?? SelectedMessage, SpecialFolder.Archive, "Archived");

    [RelayCommand]
    private Task DeleteAsync(MessageViewModel? message)
        => MoveToSpecialAsync(message ?? SelectedMessage, SpecialFolder.Deleted, "Deleted");

    [RelayCommand]
    private Task JunkAsync(MessageViewModel? message)
        => MoveToSpecialAsync(message ?? SelectedMessage, SpecialFolder.Junk, "Marked as junk");

    private async Task MoveToSpecialAsync(MessageViewModel? m, SpecialFolder dst, string verb)
    {
        if (m is null) return;
        var account = Accounts.FirstOrDefault(a => a.Model.Id == m.Model.AccountId)?.Model;
        if (account is null) return;
        var creds = _creds.Read(account.Id);
        if (creds is null) return;
        if (!uint.TryParse(m.Model.Id, out var uid)) return;

        StatusMessage = $"Moving to {dst}…";
        var res = await _imap.MoveToSpecialAsync(account, creds.Value.Password, m.Model.FolderId, uid, dst);
        if (res is Result<string>.Ok)
        {
            // Optimistic UI: drop the message from the current list.
            if (m.Unread) UpdateFolderUnreadCount(m.Model.FolderId, -1);
            Messages.Remove(m);
            if (SelectedMessage == m) SelectedMessage = Messages.FirstOrDefault();
            MarkGroupTransitions();
            StatusMessage = verb;
        }
        else if (res is Result<string>.Fail f)
        {
            StatusMessage = $"Couldn't move: {f.Error}";
        }
    }

    [RelayCommand]
    private async Task MoveToFolderAsync((MessageViewModel msg, FolderViewModel folder) p)
    {
        var (m, f) = p;
        if (m is null || f is null || string.IsNullOrEmpty(f.Model.FullPath)) return;
        var account = Accounts.FirstOrDefault(a => a.Model.Id == m.Model.AccountId)?.Model;
        if (account is null) return;
        var creds = _creds.Read(account.Id);
        if (creds is null) return;
        if (!uint.TryParse(m.Model.Id, out var uid)) return;
        if (m.Model.FolderId == f.Model.FullPath) return;

        StatusMessage = $"Moving to {f.Name}…";
        var res = await _imap.MoveToFolderAsync(account, creds.Value.Password,
            m.Model.FolderId, uid, f.Model.FullPath);
        if (res.IsOk)
        {
            if (m.Unread) UpdateFolderUnreadCount(m.Model.FolderId, -1);
            Messages.Remove(m);
            if (SelectedMessage == m) SelectedMessage = Messages.FirstOrDefault();
            MarkGroupTransitions();
            StatusMessage = $"Moved to {f.Name}.";
        }
        else
        {
            StatusMessage = $"Couldn't move: {res.AsError}";
        }
    }

    [RelayCommand]
    private async Task MarkFolderReadAsync(FolderViewModel? folder)
    {
        if (folder is null || folder.UnreadCount == 0) return;
        if (string.IsNullOrEmpty(folder.Model.FullPath)) return;

        // Walk the currently loaded message list and mark each unread one.
        // NOTE: doesn't touch messages that are on the server but not loaded
        // (the §14 fetch limit is 200). A full server-side STORE on all uids
        // lands when we add the SQLite cache.
        var unread = Messages.Where(m => m.Unread && m.Model.FolderId == folder.Model.FullPath).ToList();
        foreach (var m in unread)
            await SetReadStateAsync(m, seen: true, silent: true);
        StatusMessage = $"{folder.Name}: {unread.Count} message(s) marked read.";
    }

    [RelayCommand]
    private void CreateRuleFromMessage(MessageViewModel? message)
    {
        if (message is null) return;
        // P3-AI-1: opens a Rule Builder dialog with sender/subject pre-populated.
        // For now, surface a placeholder so the menu item is wired.
        MessageBox.Show(
            $"Rule from {message.SenderEmail}\n\nThe rules engine arrives in a future release. " +
            "It'll let you say things like 'when from this sender → move to Vendors / mark read / flag'.",
            "Create rule",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void NewSubfolder(FolderViewModel? parent)
    {
        MessageBox.Show("New subfolder UI lands soon.", "New subfolder",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void RenameFolder(FolderViewModel? folder)
    {
        MessageBox.Show("Rename folder UI lands soon.", "Rename folder",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void DeleteFolder(FolderViewModel? folder)
    {
        MessageBox.Show("Delete folder UI lands soon.", "Delete folder",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void EmptyFolder(FolderViewModel? folder)
    {
        MessageBox.Show("Empty folder UI lands soon.", "Empty folder",
            MessageBoxButton.OK, MessageBoxImage.Information);
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
