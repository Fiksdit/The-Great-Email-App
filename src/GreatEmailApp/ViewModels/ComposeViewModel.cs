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

    public ObservableCollection<Account> AvailableAccounts { get; } = new();
    [ObservableProperty] private Account? fromAccount;

    [ObservableProperty] private string toAddresses = "";
    [ObservableProperty] private string ccAddresses = "";
    [ObservableProperty] private string bccAddresses = "";
    [ObservableProperty] private bool ccBccVisible;
    [ObservableProperty] private string subject = "";
    [ObservableProperty] private string body = "";

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
        IEnumerable<Account> accounts,
        Account? defaultAccount = null)
    {
        _smtp = smtp;
        _imap = imap;
        _creds = creds;

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
            // Tack on original To + Cc, minus our own address (we don't reply to ourselves).
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

        Body = "\n\n" + QuoteForReply(original);
    }

    public void PrepareForward(Message original)
    {
        InReplyToMessageId = null;
        Subject = PrefixSubject(original.Subject, "Fwd: ");
        ToAddresses = "";
        Body = "\n\n" + QuoteForForward(original);
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

    private static string QuoteForReply(Message m)
    {
        var who = string.IsNullOrWhiteSpace(m.SenderEmail) ? m.Sender : $"{m.Sender} <{m.SenderEmail}>";
        var when = string.IsNullOrWhiteSpace(m.FullTime) ? m.Time : m.FullTime;
        var src = !string.IsNullOrEmpty(m.BodyPlain) ? m.BodyPlain : StripTags(m.BodyHtml);
        var quoted = string.Join("\n", (src ?? "").Split('\n').Select(l => "> " + l));
        return $"On {when}, {who} wrote:\n{quoted}";
    }

    private static string QuoteForForward(Message m)
    {
        var src = !string.IsNullOrEmpty(m.BodyPlain) ? m.BodyPlain : StripTags(m.BodyHtml);
        return
            "---------- Forwarded message ----------\n" +
            $"From: {m.Sender} <{m.SenderEmail}>\n" +
            $"Date: {(string.IsNullOrEmpty(m.FullTime) ? m.Time : m.FullTime)}\n" +
            $"Subject: {m.Subject}\n" +
            $"To: {m.To}\n" +
            (string.IsNullOrEmpty(m.Cc) ? "" : $"Cc: {m.Cc}\n") +
            "\n" +
            src;
    }

    private static string StripTags(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        // Crude HTML→text — good enough for quoting. Real HTML→text conversion
        // would need an HTML parser; this handles the common case where the user
        // wants to reply to a plain message that arrived as HTML.
        var noBlocks = System.Text.RegularExpressions.Regex.Replace(html, @"<(br|p|div|li)[^>]*>", "\n",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var noTags = System.Text.RegularExpressions.Regex.Replace(noBlocks, @"<[^>]+>", "");
        return System.Net.WebUtility.HtmlDecode(noTags).Trim();
    }

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

        var builder = new BodyBuilder { TextBody = Body ?? "" };
        foreach (var a in Attachments)
        {
            try { builder.Attachments.Add(a.FilePath); }
            catch { /* skip files that disappeared between picker and send */ }
        }
        msg.Body = builder.ToMessageBody();
        return msg;
    }

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
}
