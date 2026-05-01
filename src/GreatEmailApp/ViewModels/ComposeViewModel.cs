// FILE: src/GreatEmailApp/ViewModels/ComposeViewModel.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;
using MimeKit;

namespace GreatEmailApp.ViewModels;

public enum ComposeMode { New, Reply, ReplyAll, Forward }

public sealed class ComposeAttachment
{
    public required string FilePath { get; init; }
    public string FileName => System.IO.Path.GetFileName(FilePath);
    public long SizeBytes { get; init; }
    public string SizeText =>
        SizeBytes < 1024 ? $"{SizeBytes} B" :
        SizeBytes < 1024 * 1024 ? $"{SizeBytes / 1024} KB" :
        $"{SizeBytes / 1024.0 / 1024.0:0.#} MB";
}

public partial class ComposeViewModel : ObservableObject
{
    private readonly ISmtpService _smtp;
    private readonly IImapService _imap;
    private readonly ICredentialStore _creds;
    private readonly IContactsStore _contacts;
    private readonly IDraftStore _drafts;

    /// <summary>Stable id for this compose session — matches the draft row
    /// if the user saves. Lets us update vs. create on every Save.</summary>
    public string DraftId { get; set; } = Guid.NewGuid().ToString("N");

    public ObservableCollection<Account> AvailableAccounts { get; } = new();
    [ObservableProperty] private Account? fromAccount;

    [ObservableProperty] private string toAddresses = "";
    [ObservableProperty] private string ccAddresses = "";
    [ObservableProperty] private string bccAddresses = "";
    [ObservableProperty] private bool ccBccVisible;
    [ObservableProperty] private string subject = "";

    /// <summary>
    /// HTML body. Authored in the WebView2 editor. The plain-text alternative
    /// is generated from this at send time via the editor's innerText readback.
    /// Initial value carries the quoted-reply / forward header HTML.
    /// </summary>
    public string BodyHtml { get; set; } = "";

    /// <summary>
    /// Cached innerText of the editor — kept fresh by ComposeWindow on
    /// every editor change. Used for Send.CanExecute and the multipart text
    /// alternative.
    /// </summary>
    [ObservableProperty] private string bodyText = "";

    public ObservableCollection<ComposeAttachment> Attachments { get; } = new();

    [ObservableProperty] private bool isSending;
    [ObservableProperty] private string statusMessage = "";

    /// <summary>If non-null, send will set In-Reply-To / References headers
    /// from this Message-Id so the conversation threads correctly.</summary>
    public string? InReplyToMessageId { get; set; }

    public IAsyncRelayCommand SendCommand { get; }
    public IRelayCommand AddAttachmentCommand { get; }
    public IRelayCommand<ComposeAttachment> RemoveAttachmentCommand { get; }
    public IRelayCommand ToggleCcBccCommand { get; }

    public event EventHandler? Sent;

    public ComposeViewModel(
        ISmtpService smtp,
        IImapService imap,
        ICredentialStore creds,
        IContactsStore contacts,
        IDraftStore drafts,
        IEnumerable<Account> accounts,
        Account? defaultAccount = null)
    {
        _smtp = smtp;
        _imap = imap;
        _creds = creds;
        _contacts = contacts;
        _drafts = drafts;

        foreach (var a in accounts) AvailableAccounts.Add(a);
        fromAccount = defaultAccount
            ?? AvailableAccounts.FirstOrDefault(a => a.IsPrimary)
            ?? AvailableAccounts.FirstOrDefault();

        SendCommand = new AsyncRelayCommand(SendAsync, CanSend);
        AddAttachmentCommand = new RelayCommand(AddAttachment);
        RemoveAttachmentCommand = new RelayCommand<ComposeAttachment>(RemoveAttachment);
        ToggleCcBccCommand = new RelayCommand(() => CcBccVisible = !CcBccVisible);
    }

    partial void OnFromAccountChanged(Account? value) => SendCommand.NotifyCanExecuteChanged();
    partial void OnToAddressesChanged(string value)   => SendCommand.NotifyCanExecuteChanged();
    partial void OnIsSendingChanged(bool value)       => SendCommand.NotifyCanExecuteChanged();

    private bool CanSend() =>
        !IsSending && FromAccount is not null && !string.IsNullOrWhiteSpace(ToAddresses);

    // --------------------------------------------------------------------- //
    // Pre-populate factories — call after construction.
    // --------------------------------------------------------------------- //

    public void PrepareReply(Message original, bool replyAll)
    {
        InReplyToMessageId = original.Id;
        Subject = PrefixSubject(original.Subject, "Re: ");

        ToAddresses = string.IsNullOrWhiteSpace(original.SenderEmail)
            ? original.Sender : original.SenderEmail;

        if (replyAll)
        {
            var ours = (FromAccount?.EmailAddress ?? "").ToLowerInvariant();
            var extras = new List<string>();
            extras.AddRange(SplitAddresses(original.To));
            extras.AddRange(SplitAddresses(original.Cc));
            extras = extras
                .Where(x => !string.Equals(x, ours, StringComparison.OrdinalIgnoreCase))
                .Where(x => !string.Equals(x, original.SenderEmail, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            CcAddresses = string.Join(", ", extras);
            CcBccVisible = !string.IsNullOrWhiteSpace(CcAddresses);
        }

        BodyHtml = "<p><br></p>" + QuoteForReplyHtml(original);
    }

    public void PrepareForward(Message original)
    {
        InReplyToMessageId = null;
        Subject = PrefixSubject(original.Subject, "Fwd: ");
        ToAddresses = "";
        BodyHtml = "<p><br></p>" + QuoteForForwardHtml(original);
    }

    private static string PrefixSubject(string subject, string prefix)
    {
        if (string.IsNullOrWhiteSpace(subject)) return prefix.TrimEnd();
        return subject.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? subject : prefix + subject;
    }

    private static IEnumerable<string> SplitAddresses(string raw) =>
        (raw ?? "")
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0);

    private static string QuoteForReplyHtml(Message m)
    {
        var who = string.IsNullOrWhiteSpace(m.SenderEmail) ? m.Sender : $"{m.Sender} &lt;{m.SenderEmail}&gt;";
        var when = string.IsNullOrWhiteSpace(m.FullTime) ? m.Time : m.FullTime;
        var inner = !string.IsNullOrEmpty(m.BodyHtml) ? m.BodyHtml : Escape(m.BodyPlain).Replace("\n", "<br>");
        return
            $"<div>On {System.Net.WebUtility.HtmlEncode(when)}, {who} wrote:</div>" +
            $"<blockquote style=\"margin:0 0 0 .8ex;border-left:3px solid #3a3a3a;padding-left:1ex;\">{inner}</blockquote>";
    }

    private static string QuoteForForwardHtml(Message m)
    {
        var inner = !string.IsNullOrEmpty(m.BodyHtml) ? m.BodyHtml : Escape(m.BodyPlain).Replace("\n", "<br>");
        var when = string.IsNullOrEmpty(m.FullTime) ? m.Time : m.FullTime;
        return
            "<div>---------- Forwarded message ----------</div>" +
            $"<div><b>From:</b> {Escape(m.Sender)} &lt;{Escape(m.SenderEmail)}&gt;</div>" +
            $"<div><b>Date:</b> {Escape(when)}</div>" +
            $"<div><b>Subject:</b> {Escape(m.Subject)}</div>" +
            $"<div><b>To:</b> {Escape(m.To)}</div>" +
            (string.IsNullOrEmpty(m.Cc) ? "" : $"<div><b>Cc:</b> {Escape(m.Cc)}</div>") +
            "<br>" +
            inner;
    }

    private static string Escape(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");

    // --------------------------------------------------------------------- //
    // Send
    // --------------------------------------------------------------------- //

    private async Task SendAsync()
    {
        var account = FromAccount;
        if (account is null) return;
        var creds = _creds.Read(account.Id);
        if (creds is null)
        {
            StatusMessage = "No password stored for this account. Re-add it in Settings → Accounts.";
            return;
        }

        IsSending = true;
        StatusMessage = "Sending…";
        try
        {
            var mime = BuildMimeMessage(account);
            var send = await _smtp.SendAsync(account, creds.Value.Password, mime);
            if (send is Result<bool>.Fail f)
            {
                StatusMessage = $"Send failed: {f.Error}";
                return;
            }

            // Best-effort copy to Sent. Don't fail the send if it doesn't work —
            // the message has already left the building.
            _ = Task.Run(async () => await _imap.AppendToSentAsync(account, creds.Value.Password, mime));

            // Auto-collect: any recipient address that isn't already in the
            // contacts roster gets added as AutoCollected. Quiet by design —
            // the user can review/clean these in Settings → Contacts.
            try { AutoCollectRecipients(); } catch { /* contacts is best-effort */ }

            // Send succeeded — clean up any draft row this compose was working from.
            try { _drafts.Delete(DraftId); } catch { /* best-effort */ }

            StatusMessage = "Sent.";
            Sent?.Invoke(this, EventArgs.Empty);
        }
        finally { IsSending = false; }
    }

    private MimeMessage BuildMimeMessage(Account from)
    {
        var msg = new MimeMessage();
        msg.From.Add(new MailboxAddress(
            string.IsNullOrWhiteSpace(from.DisplayName) ? from.EmailAddress : from.DisplayName,
            from.EmailAddress));
        AddTo(msg.To,  ToAddresses);
        AddTo(msg.Cc,  CcAddresses);
        AddTo(msg.Bcc, BccAddresses);
        msg.Subject = Subject ?? "";

        if (!string.IsNullOrEmpty(InReplyToMessageId))
        {
            msg.InReplyTo = InReplyToMessageId;
            msg.References.Add(InReplyToMessageId);
        }

        // Build multipart/alternative — text/plain for old clients, text/html for everyone else.
        var builder = new BodyBuilder
        {
            TextBody = string.IsNullOrEmpty(BodyText) ? "" : BodyText,
            HtmlBody = string.IsNullOrEmpty(BodyHtml) ? "" : WrapHtmlForSend(BodyHtml),
        };
        foreach (var a in Attachments)
        {
            try { builder.Attachments.Add(a.FilePath); }
            catch { /* skip files that disappeared between picker and send */ }
        }
        msg.Body = builder.ToMessageBody();
        return msg;
    }

    /// <summary>
    /// Wrap the editor's innerHTML in a minimal HTML document. We don't ship a
    /// dark theme to recipients — they'll have their own client styling.
    /// </summary>
    private static string WrapHtmlForSend(string innerHtml) =>
        $"<!doctype html><html><head><meta charset=\"utf-8\"></head><body>{innerHtml}</body></html>";

    private void AutoCollectRecipients()
    {
        var existingEmails = _contacts.LoadAll()
            .Select(c => c.EmailAddress)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in new[] { ToAddresses, CcAddresses, BccAddresses })
        {
            foreach (var addr in SplitAddresses(raw))
            {
                if (!MailboxAddress.TryParse(addr, out var parsed)) continue;
                if (existingEmails.Contains(parsed.Address)) continue;
                _contacts.AddOrGet(new Contact
                {
                    Id = Guid.NewGuid().ToString("N"),
                    EmailAddress = parsed.Address,
                    DisplayName = parsed.Name ?? "",
                    AutoCollected = true,
                });
                existingEmails.Add(parsed.Address);
            }
        }
    }

    /// <summary>
    /// Returns up to <paramref name="limit"/> contacts whose name or email
    /// starts with / contains the partial token. Used by the To/Cc/Bcc autocomplete.
    /// </summary>
    public IReadOnlyList<Contact> SuggestContacts(string partial, int limit = 8)
    {
        if (string.IsNullOrWhiteSpace(partial)) return Array.Empty<Contact>();
        var p = partial.Trim();
        return _contacts.LoadAll()
            .Where(c => c.EmailAddress.Contains(p, StringComparison.OrdinalIgnoreCase)
                     || (c.DisplayName ?? "").Contains(p, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => Starts(c.EmailAddress, p) || Starts(c.DisplayName ?? "", p))
            .ThenByDescending(c => !c.AutoCollected) // prefer manually entered
            .ThenBy(c => c.DisplayName)
            .Take(limit)
            .ToList();
    }

    private static bool Starts(string s, string p) => s.StartsWith(p, StringComparison.OrdinalIgnoreCase);

    private static void AddTo(InternetAddressList list, string raw)
    {
        foreach (var addr in SplitAddresses(raw))
        {
            if (MailboxAddress.TryParse(addr, out var parsed)) list.Add(parsed);
        }
    }

    // --------------------------------------------------------------------- //
    // Attachment commands
    // --------------------------------------------------------------------- //

    private void AddAttachment()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Title = "Attach files",
        };
        if (dlg.ShowDialog() != true) return;
        foreach (var path in dlg.FileNames)
        {
            try
            {
                var fi = new System.IO.FileInfo(path);
                Attachments.Add(new ComposeAttachment { FilePath = fi.FullName, SizeBytes = fi.Length });
            }
            catch { /* skip unreadable */ }
        }
    }

    private void RemoveAttachment(ComposeAttachment? a)
    {
        if (a is null) return;
        Attachments.Remove(a);
    }

    // --------------------------------------------------------------------- //
    // Drafts
    // --------------------------------------------------------------------- //

    /// <summary>True if there's anything worth persisting / asking the user about.</summary>
    public bool HasContent =>
        !string.IsNullOrWhiteSpace(ToAddresses) ||
        !string.IsNullOrWhiteSpace(CcAddresses) ||
        !string.IsNullOrWhiteSpace(BccAddresses) ||
        !string.IsNullOrWhiteSpace(Subject) ||
        !string.IsNullOrWhiteSpace(BodyText) ||
        Attachments.Count > 0;

    public void SaveAsDraft()
    {
        var draft = new Draft
        {
            Id = DraftId,
            AccountId = FromAccount?.Id ?? "",
            To = ToAddresses ?? "",
            Cc = CcAddresses ?? "",
            Bcc = BccAddresses ?? "",
            Subject = Subject ?? "",
            BodyHtml = BodyHtml ?? "",
            BodyText = BodyText ?? "",
            InReplyToMessageId = InReplyToMessageId,
            AttachmentPaths = Attachments.Select(a => a.FilePath).ToList(),
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _drafts.Save(draft);
        StatusMessage = $"Draft saved at {DateTime.Now:t}.";
    }

    /// <summary>Populate this VM from an existing Draft row.</summary>
    public void LoadDraft(Draft d)
    {
        DraftId = d.Id;
        var matchingAccount = AvailableAccounts.FirstOrDefault(a => a.Id == d.AccountId);
        if (matchingAccount is not null) FromAccount = matchingAccount;
        ToAddresses  = d.To  ?? "";
        CcAddresses  = d.Cc  ?? "";
        BccAddresses = d.Bcc ?? "";
        if (!string.IsNullOrWhiteSpace(CcAddresses) || !string.IsNullOrWhiteSpace(BccAddresses))
            CcBccVisible = true;
        Subject = d.Subject ?? "";
        BodyHtml = d.BodyHtml ?? "";
        BodyText = d.BodyText ?? "";
        InReplyToMessageId = d.InReplyToMessageId;
        Attachments.Clear();
        foreach (var path in d.AttachmentPaths)
        {
            try
            {
                var fi = new System.IO.FileInfo(path);
                Attachments.Add(new ComposeAttachment
                {
                    FilePath = fi.FullName,
                    SizeBytes = fi.Exists ? fi.Length : 0,
                });
            }
            catch
            {
                // File gone — keep the path so the user can re-pick if they want.
                Attachments.Add(new ComposeAttachment { FilePath = path, SizeBytes = 0 });
            }
        }
    }

    /// <summary>Remove this compose session's draft row, if any.</summary>
    public void DeleteDraft()
    {
        try { _drafts.Delete(DraftId); } catch { }
    }
}
