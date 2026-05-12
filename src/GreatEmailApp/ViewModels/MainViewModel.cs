// FILE: src/GreatEmailApp/ViewModels/MainViewModel.cs
// Created: 2026-04-29 | Revised: 2026-05-12 | Rev: 6
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

    // Avatar shows the signed-in Firebase identity. When signed out we deliberately
    // show "?" + "Sign in" so the avatar prompts the user instead of pretending an
    // account is active. TitleBar's click handler routes to Settings → Sync in that case.
    public string AccountInitial
    {
        get
        {
            var s = App.Auth?.Current;
            if (s is null) return "?";
            var name = !string.IsNullOrWhiteSpace(s.DisplayName) ? s.DisplayName : s.Email;
            return string.IsNullOrEmpty(name) ? "?" : char.ToUpperInvariant(name[0]).ToString();
        }
    }
    public string AccountEmail => App.Auth?.Current?.Email ?? "Sign in";
    public bool IsSignedIn => App.Auth?.IsSignedIn ?? false;

    private CancellationTokenSource? _messageLoadCts;
    private CancellationTokenSource? _bodyLoadCts;
    private DispatcherTimer? _markReadTimer;
    private MessageViewModel? _markReadPending;

    public MainViewModel() : this(App.Imap, App.Credentials, App.Accounts) { }

    private bool _draftSubscribed;

    /// <summary>Total local draft count across all accounts. Drives the
    /// ribbon Drafts button's badge chip.</summary>
    [ObservableProperty] private int draftCount;

    public bool HasDrafts => DraftCount > 0;
    partial void OnDraftCountChanged(int value) => OnPropertyChanged(nameof(HasDrafts));

    // Filter pill + search-box plumbing. Both refresh the same default
    // ICollectionView; the predicate combines pill-state AND search-text.
    partial void OnFilterChanged(string value) =>
        System.Windows.Data.CollectionViewSource.GetDefaultView(Messages)?.Refresh();

    partial void OnSearchTextChanged(string value) =>
        System.Windows.Data.CollectionViewSource.GetDefaultView(Messages)?.Refresh();

    /// <summary>
    /// Basic in-folder search. STRICT: matches only against sender display
    /// name, sender email address, and subject. Never the body — the user
    /// explicitly wants to avoid false positives like "sentrix" hitting
    /// thousands of newsletter mentions when they're hunting for "mobile
    /// sentrix" orders. Full-text / advanced search lives in roadmap P1-15+.
    /// Case-insensitive partial match.
    /// </summary>
    private bool MessageMatchesFilter(object o)
    {
        if (o is not MessageViewModel m) return false;

        // Pill predicate first (cheaper).
        var passesPill = Filter switch
        {
            "Unread"   => m.Unread,
            "Flagged"  => m.Flagged,
            "Mentions" => true, // No data backing this yet — pass-through until the feature lands.
            _          => true,  // "All" or anything unrecognized.
        };
        if (!passesPill) return false;

        var q = SearchText;
        if (string.IsNullOrWhiteSpace(q)) return true;

        // Strict-fields contains-check. Three independent text fields, OR'd.
        return ContainsCI(m.Sender, q)
            || ContainsCI(m.SenderEmail, q)
            || ContainsCI(m.Subject, q);
    }

    private static bool ContainsCI(string? haystack, string needle) =>
        !string.IsNullOrEmpty(haystack)
        && haystack.Contains(needle, System.StringComparison.OrdinalIgnoreCase);

    public MainViewModel(IImapService imap, ICredentialStore creds, IAccountStore accountStore)
    {
        _imap = imap;
        _creds = creds;
        _accountStore = accountStore;
        LoadAccounts();

        // Wire the All/Unread/Flagged/Mentions pills to actually filter the list.
        // The pills set MainViewModel.Filter; OnFilterChanged below refreshes the
        // collection view so the predicate runs again. Without this hook the pills
        // toggled state but never hid anything.
        var view = System.Windows.Data.CollectionViewSource.GetDefaultView(Messages);
        if (view is not null) view.Filter = MessageMatchesFilter;

        // Subscribe once for the lifetime of the VM (App.Drafts is a singleton).
        if (!_draftSubscribed && App.Drafts is not null)
        {
            App.Drafts.Changed += (_, _) =>
                Application.Current?.Dispatcher.BeginInvoke(new Action(RefreshDraftCount));
            _draftSubscribed = true;
            RefreshDraftCount();
        }

        // Repaint the avatar when sign-in state changes (Settings → Sync → Google sign-in,
        // sign-out, or silent restore at startup).
        if (App.Auth is not null)
        {
            App.Auth.SessionChanged += (_, _) =>
                Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
                {
                    OnPropertyChanged(nameof(AccountInitial));
                    OnPropertyChanged(nameof(AccountEmail));
                    OnPropertyChanged(nameof(IsSignedIn));
                }));
        }
    }

    private void RefreshDraftCount() => DraftCount = App.Drafts?.LoadAll().Count ?? 0;

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

        // Paint cached folders immediately so the sidebar isn't blank during the
        // 1–3s IMAP LIST round-trip. The live fetch below will replace them once
        // the server answers.
        var cached = App.FolderCache.Load(account.Id);
        if (cached.Count > 0 && accountVm.Folders.Count == 0)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var f in cached)
                {
                    f.AccountId = account.Id;
                    accountVm.Folders.Add(new FolderViewModel(f));
                }
            });
        }

        var result = await _imap.ListFoldersAsync(account, creds.Value.Password);
        if (result is Result<System.Collections.Generic.List<Folder>>.Ok ok)
        {
            account.Status = AccountStatus.Connected;
            // Persist for next launch — local-only, never synced.
            App.FolderCache.Save(account.Id, ok.Value);
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
        var msg = message.Model;

        // Prefer matching on the sender domain — covers every email from
        // mobilesentrix.com, not just the specific autoresponder address.
        var fromValue = msg.SenderEmail;
        var atIdx = fromValue.IndexOf('@');
        if (atIdx > 0 && atIdx < fromValue.Length - 1)
            fromValue = fromValue[(atIdx + 1)..];        // domain only
        else if (string.IsNullOrEmpty(fromValue))
            fromValue = msg.Sender ?? "";                // fallback to display name

        // Suggested rule name: prefer the display name, fall back to the domain.
        var ruleName = string.IsNullOrWhiteSpace(msg.Sender) ? fromValue : msg.Sender;

        // Suggested folder name: TitleCase of the domain root, e.g. mobilesentrix → Mobilesentrix.
        // The user can rename in the editor — this is just a starting point.
        var domainRoot = fromValue.Split('.').FirstOrDefault() ?? fromValue;
        var folderSuggestion = string.IsNullOrEmpty(domainRoot)
            ? ""
            : char.ToUpper(domainRoot[0]) + domainRoot[1..];

        var seed = new MailRule
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = ruleName,
            IsEnabled = true,
            AccountId = string.IsNullOrEmpty(msg.AccountId) ? null : msg.AccountId,
            Match = RuleMatch.All,
            Conditions = new List<RuleCondition>
            {
                new() { Field = RuleField.From, Op = RuleOp.Contains, Value = fromValue },
            },
            Actions = new List<RuleActionItem>
            {
                new() { Type = RuleAction.MoveToFolder, Value = folderSuggestion },
            },
        };

        // Build the per-account folder dictionary (same pattern Settings dialog
        // uses) so the editor's folder picker shows real choices.
        var foldersByAccount = new Dictionary<string, IReadOnlyList<string>>();
        foreach (var avm in Accounts)
        {
            var paths = new List<string>();
            CollectFolderPaths(avm.Folders, paths);
            foldersByAccount[avm.Model.Id] = paths;
        }

        var dlg = new GreatEmailApp.Views.Dialogs.RuleEditorDialog(seed, App.Accounts.LoadAll().ToList(), foldersByAccount)
        {
            Owner = Application.Current.MainWindow,
        };
        if (dlg.ShowDialog() == true)
        {
            var rules = App.Rules.LoadAll().ToList();
            rules.Add(dlg.Result);
            App.Rules.Save(rules);
            StatusMessage = $"Rule '{dlg.Result.Name}' created.";
        }
    }

    private static void CollectFolderPaths(
        ObservableCollection<FolderViewModel> folders, List<string> sink)
    {
        foreach (var f in folders)
        {
            if (!string.IsNullOrEmpty(f.Model.FullPath)) sink.Add(f.Model.FullPath);
            CollectFolderPaths(f.Children, sink);
        }
    }

    // ─── Folder CRUD ──────────────────────────────────────────────────
    // All four commands share the same shape: prompt/confirm → IMAP call →
    // reload the account's folder tree via LoadFoldersAsync so the sidebar
    // mirrors server state. Special folders (Inbox/Sent/Drafts/Junk/Trash/
    // Archive) are protected from Rename/Delete to avoid breaking the
    // \Special-Use semantics other parts of the app rely on.

    [RelayCommand]
    private async Task NewSubfolderAsync(FolderViewModel? parent)
    {
        if (parent is null) return;
        var accountVm = Accounts.FirstOrDefault(a => a.Model.Id == parent.Model.AccountId);
        if (accountVm is null) return;

        var dlg = new Views.Dialogs.FolderNameDialog(
            titleText: "New subfolder",
            helpText: $"Create a new folder inside \"{parent.Name}\".")
        {
            Owner = Application.Current?.MainWindow,
        };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Name)) return;

        await RunFolderOpAsync(accountVm, $"Creating \"{dlg.Name}\"…",
            async (account, password, ct) =>
            {
                var r = await _imap.CreateFolderAsync(account, password, parent.Model.FullPath, dlg.Name, ct);
                return r is Result<string>.Ok ok
                    ? (true, $"Created \"{ok.Value}\".")
                    : (false, $"Create failed: {((Result<string>.Fail)r).Error}");
            });
    }

    /// <summary>Create a top-level folder under the account's root namespace.
    /// Invoked from the account-header context menu in the sidebar.</summary>
    [RelayCommand]
    private async Task NewTopLevelFolderAsync(AccountViewModel? accountVm)
    {
        if (accountVm is null) return;

        var dlg = new Views.Dialogs.FolderNameDialog(
            titleText: "New folder",
            helpText: $"Create a new top-level folder in {accountVm.EmailAddress}.")
        {
            Owner = Application.Current?.MainWindow,
        };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Name)) return;

        await RunFolderOpAsync(accountVm, $"Creating \"{dlg.Name}\"…",
            async (account, password, ct) =>
            {
                var r = await _imap.CreateFolderAsync(account, password, parentFullPath: "", dlg.Name, ct);
                return r is Result<string>.Ok ok
                    ? (true, $"Created \"{ok.Value}\".")
                    : (false, $"Create failed: {((Result<string>.Fail)r).Error}");
            });
    }

    [RelayCommand]
    private async Task RenameFolderAsync(FolderViewModel? folder)
    {
        if (folder is null) return;
        if (folder.Model.Special != SpecialFolder.None)
        {
            MessageBox.Show($"Can't rename the {folder.Name} folder — it's a special-use folder.",
                "Rename folder", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var accountVm = Accounts.FirstOrDefault(a => a.Model.Id == folder.Model.AccountId);
        if (accountVm is null) return;

        var dlg = new Views.Dialogs.FolderNameDialog(
            titleText: "Rename folder",
            helpText: $"Enter a new name for \"{folder.Name}\".",
            initialName: folder.Name)
        {
            Owner = Application.Current?.MainWindow,
        };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Name)) return;
        if (string.Equals(dlg.Name, folder.Name, StringComparison.Ordinal)) return;

        var wasSelected = SelectedFolder == folder;
        await RunFolderOpAsync(accountVm, $"Renaming to \"{dlg.Name}\"…",
            async (account, password, ct) =>
            {
                var r = await _imap.RenameFolderAsync(account, password, folder.Model.FullPath, dlg.Name, ct);
                return r is Result<string>.Ok ok
                    ? (true, $"Renamed to \"{ok.Value}\".")
                    : (false, $"Rename failed: {((Result<string>.Fail)r).Error}");
            });

        // The selected-folder reference points at the old VM, which we just
        // rebuilt. Clear to a safe state — the user can click the renamed
        // folder in the refreshed tree.
        if (wasSelected)
        {
            SelectedFolder = null;
            Messages.Clear();
            SelectedMessage = null;
        }
    }

    [RelayCommand]
    private async Task DeleteFolderAsync(FolderViewModel? folder)
    {
        if (folder is null) return;
        if (folder.Model.Special != SpecialFolder.None)
        {
            MessageBox.Show($"Can't delete the {folder.Name} folder — it's a special-use folder.",
                "Delete folder", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var accountVm = Accounts.FirstOrDefault(a => a.Model.Id == folder.Model.AccountId);
        if (accountVm is null) return;

        var confirm = MessageBox.Show(
            $"Delete the folder \"{folder.Name}\"?\n\n" +
            "This removes the folder from the server. Messages it contains will be deleted. " +
            "This cannot be undone.",
            "Delete folder", MessageBoxButton.OKCancel, MessageBoxImage.Warning,
            MessageBoxResult.Cancel);
        if (confirm != MessageBoxResult.OK) return;

        var wasSelected = SelectedFolder == folder;
        await RunFolderOpAsync(accountVm, $"Deleting \"{folder.Name}\"…",
            async (account, password, ct) =>
            {
                var r = await _imap.DeleteFolderAsync(account, password, folder.Model.FullPath, ct);
                return r is Result<bool>.Ok
                    ? (true, $"Deleted \"{folder.Name}\".")
                    : (false, $"Delete failed: {((Result<bool>.Fail)r).Error}");
            });

        if (wasSelected)
        {
            SelectedFolder = null;
            Messages.Clear();
            SelectedMessage = null;
        }
    }

    [RelayCommand]
    private async Task EmptyFolderAsync(FolderViewModel? folder)
    {
        if (folder is null) return;
        var accountVm = Accounts.FirstOrDefault(a => a.Model.Id == folder.Model.AccountId);
        if (accountVm is null) return;

        var confirm = MessageBox.Show(
            $"Permanently delete every message in \"{folder.Name}\"?\n\n" +
            "This empties the folder on the server. This cannot be undone.",
            "Empty folder", MessageBoxButton.OKCancel, MessageBoxImage.Warning,
            MessageBoxResult.Cancel);
        if (confirm != MessageBoxResult.OK) return;

        var wasSelected = SelectedFolder == folder;
        await RunFolderOpAsync(accountVm, $"Emptying \"{folder.Name}\"…",
            async (account, password, ct) =>
            {
                var r = await _imap.EmptyFolderAsync(account, password, folder.Model.FullPath, ct);
                return r is Result<int>.Ok ok
                    ? (true, $"Emptied \"{folder.Name}\" ({ok.Value} message(s) removed).")
                    : (false, $"Empty failed: {((Result<int>.Fail)r).Error}");
            });

        if (wasSelected)
        {
            // Refresh the message list for this folder — empty now.
            Messages.Clear();
            SelectedMessage = null;
        }
    }

    /// <summary>Shared scaffolding for folder CRUD: pulls creds, runs the op,
    /// surfaces the result on StatusMessage, then reloads the folder tree so
    /// the sidebar reflects server state. <paramref name="op"/> returns
    /// (success, statusMessage).</summary>
    private async Task RunFolderOpAsync(
        AccountViewModel accountVm,
        string busyMessage,
        Func<Account, string, CancellationToken, Task<(bool ok, string status)>> op)
    {
        var account = accountVm.Model;
        var creds = _creds.Read(account.Id);
        if (creds is null)
        {
            StatusMessage = $"No password stored for {account.EmailAddress}.";
            return;
        }

        IsBusy = true;
        StatusMessage = busyMessage;
        try
        {
            var (_, status) = await op(account, creds.Value.Password, CancellationToken.None);
            StatusMessage = status;
        }
        finally
        {
            IsBusy = false;
        }

        // Always refresh the folder tree on completion — even on failure the
        // server state may have changed (partial ops, race with another client).
        await LoadFoldersAsync(accountVm);
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
