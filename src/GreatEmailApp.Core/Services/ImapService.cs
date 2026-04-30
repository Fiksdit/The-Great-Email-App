// FILE: src/GreatEmailApp.Core/Services/ImapService.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
// MailKit-backed IMAP. Single-shot operations: open → do → close. We do NOT
// hold a long-lived connection in Phase 2 — IDLE / push lands in Phase 5.

using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MailKit.Security;
using GreatEmailApp.Core.Models;
using MimeKit;

namespace GreatEmailApp.Core.Services;

public sealed class ImapService : IImapService
{
    public async Task<Result<bool>> TestConnectionAsync(Account account, string password, CancellationToken ct = default)
    {
        try
        {
            using var client = new ImapClient();
            await ConnectAndAuthenticateAsync(client, account, password, ct);
            await client.DisconnectAsync(true, ct);
            return Result.Ok(true);
        }
        catch (Exception ex)
        {
            return Result.Fail<bool>(SanitizeError(ex), ex);
        }
    }

    public async Task<Result<List<Folder>>> ListFoldersAsync(Account account, string password, CancellationToken ct = default)
    {
        try
        {
            using var client = new ImapClient();
            await ConnectAndAuthenticateAsync(client, account, password, ct);

            var result = new List<Folder>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // NOTE: many servers (Dovecot, fiksdit.com) place INBOX OUTSIDE the personal
            // namespace's subfolder tree — it sits at the root. Always include it explicitly.
            try
            {
                var inbox = client.Inbox;
                await inbox.StatusAsync(StatusItems.Count | StatusItems.Unread, ct);
                result.Add(new Folder
                {
                    Id = inbox.FullName,
                    Name = "Inbox",
                    AccountId = account.Id,
                    FullPath = inbox.FullName,
                    Special = Models.SpecialFolder.Inbox,
                    UnreadCount = inbox.Unread,
                    TotalCount = inbox.Count,
                    IsNested = false,
                });
                seen.Add(inbox.FullName);
            }
            catch { /* if INBOX is genuinely missing the recursion below will catch it */ }

            var personal = client.GetFolder(client.PersonalNamespaces[0]);
            await CollectFoldersTreeAsync(personal, result, account.Id, isNested: false, ct, seen);

            await client.DisconnectAsync(true, ct);
            return Result.Ok(result);
        }
        catch (Exception ex)
        {
            return Result.Fail<List<Folder>>(SanitizeError(ex), ex);
        }
    }

    public async Task<Result<List<Message>>> ListMessagesAsync(
        Account account, string password, string folderFullPath,
        int limit = 200, CancellationToken ct = default)
    {
        try
        {
            using var client = new ImapClient();
            await ConnectAndAuthenticateAsync(client, account, password, ct);

            var folder = await GetFolderByPathAsync(client, folderFullPath, ct);
            if (folder is null)
            {
                await client.DisconnectAsync(true, ct);
                return Result.Fail<List<Message>>($"Folder not found: {folderFullPath}");
            }

            await folder.OpenAsync(FolderAccess.ReadOnly, ct);

            int total = folder.Count;
            if (total == 0)
            {
                await client.DisconnectAsync(true, ct);
                return Result.Ok(new List<Message>());
            }

            // §14 — fetch the most recent `limit` items by sequence number.
            int start = Math.Max(0, total - limit);
            var summaries = await folder.FetchAsync(start, -1,
                MessageSummaryItems.Envelope |
                MessageSummaryItems.Flags |
                MessageSummaryItems.UniqueId |
                MessageSummaryItems.PreviewText |
                MessageSummaryItems.BodyStructure,
                ct);

            var messages = summaries
                .OrderByDescending(s => s.Date)
                .Take(limit) // §14
                .Select(s => Map(s, account.Id, folderFullPath))
                .ToList();

            await client.DisconnectAsync(true, ct);
            return Result.Ok(messages);
        }
        catch (Exception ex)
        {
            return Result.Fail<List<Message>>(SanitizeError(ex), ex);
        }
    }

    public async Task<Result<bool>> SetSeenAsync(
        Account account, string password, string folderFullPath, uint uid, bool seen,
        CancellationToken ct = default)
    {
        try
        {
            using var client = new ImapClient();
            await ConnectAndAuthenticateAsync(client, account, password, ct);
            var folder = await GetFolderByPathAsync(client, folderFullPath, ct);
            if (folder is null) return Result.Fail<bool>($"Folder not found: {folderFullPath}");
            await folder.OpenAsync(FolderAccess.ReadWrite, ct);

            var ids = new[] { new UniqueId(uid) };
            if (seen)
                await folder.AddFlagsAsync(ids, MessageFlags.Seen, silent: true, ct);
            else
                await folder.RemoveFlagsAsync(ids, MessageFlags.Seen, silent: true, ct);

            await client.DisconnectAsync(true, ct);
            return Result.Ok(true);
        }
        catch (Exception ex) { return Result.Fail<bool>(SanitizeError(ex), ex); }
    }

    public async Task<Result<bool>> SetFlaggedAsync(
        Account account, string password, string folderFullPath, uint uid, bool flagged,
        CancellationToken ct = default)
    {
        try
        {
            using var client = new ImapClient();
            await ConnectAndAuthenticateAsync(client, account, password, ct);
            var folder = await GetFolderByPathAsync(client, folderFullPath, ct);
            if (folder is null) return Result.Fail<bool>($"Folder not found: {folderFullPath}");
            await folder.OpenAsync(FolderAccess.ReadWrite, ct);

            var ids = new[] { new UniqueId(uid) };
            if (flagged)
                await folder.AddFlagsAsync(ids, MessageFlags.Flagged, silent: true, ct);
            else
                await folder.RemoveFlagsAsync(ids, MessageFlags.Flagged, silent: true, ct);

            await client.DisconnectAsync(true, ct);
            return Result.Ok(true);
        }
        catch (Exception ex) { return Result.Fail<bool>(SanitizeError(ex), ex); }
    }

    public async Task<Result<bool>> MoveToFolderAsync(
        Account account, string password, string srcFolderFullPath, uint uid, string dstFolderFullPath,
        CancellationToken ct = default)
    {
        try
        {
            using var client = new ImapClient();
            await ConnectAndAuthenticateAsync(client, account, password, ct);
            var src = await GetFolderByPathAsync(client, srcFolderFullPath, ct);
            if (src is null) return Result.Fail<bool>($"Source folder not found: {srcFolderFullPath}");
            var dst = await GetFolderByPathAsync(client, dstFolderFullPath, ct);
            if (dst is null) return Result.Fail<bool>($"Destination folder not found: {dstFolderFullPath}");

            await src.OpenAsync(FolderAccess.ReadWrite, ct);
            await src.MoveToAsync(new UniqueId(uid), dst, ct);
            await client.DisconnectAsync(true, ct);
            return Result.Ok(true);
        }
        catch (Exception ex) { return Result.Fail<bool>(SanitizeError(ex), ex); }
    }

    public async Task<Result<string>> MoveToSpecialAsync(
        Account account, string password, string srcFolderFullPath, uint uid,
        Models.SpecialFolder dst, CancellationToken ct = default)
    {
        try
        {
            using var client = new ImapClient();
            await ConnectAndAuthenticateAsync(client, account, password, ct);

            // Find the destination folder by IMAP \Special-Use, with a fallback
            // to common name patterns since not every server flags them.
            IMailFolder? dstFolder = TryGetSpecial(client, dst);
            dstFolder ??= await FindByNameAsync(client, dst, ct);
            if (dstFolder is null)
                return Result.Fail<string>($"No {dst} folder found on this account.");

            var src = await GetFolderByPathAsync(client, srcFolderFullPath, ct);
            if (src is null) return Result.Fail<string>($"Source folder not found: {srcFolderFullPath}");

            await src.OpenAsync(FolderAccess.ReadWrite, ct);
            await src.MoveToAsync(new UniqueId(uid), dstFolder, ct);
            await client.DisconnectAsync(true, ct);
            return Result.Ok(dstFolder.FullName);
        }
        catch (Exception ex) { return Result.Fail<string>(SanitizeError(ex), ex); }
    }

    private static IMailFolder? TryGetSpecial(ImapClient client, Models.SpecialFolder s)
    {
        try
        {
            return s switch
            {
                Models.SpecialFolder.Archive => client.GetFolder(MailKit.SpecialFolder.Archive),
                Models.SpecialFolder.Drafts  => client.GetFolder(MailKit.SpecialFolder.Drafts),
                Models.SpecialFolder.Sent    => client.GetFolder(MailKit.SpecialFolder.Sent),
                Models.SpecialFolder.Junk    => client.GetFolder(MailKit.SpecialFolder.Junk),
                Models.SpecialFolder.Deleted => client.GetFolder(MailKit.SpecialFolder.Trash),
                _ => null,
            };
        }
        catch { return null; }
    }

    private static async Task<IMailFolder?> FindByNameAsync(
        ImapClient client, Models.SpecialFolder s, CancellationToken ct)
    {
        var personal = client.GetFolder(client.PersonalNamespaces[0]);
        var candidates = s switch
        {
            Models.SpecialFolder.Archive => new[] { "Archive", "Archives", "All Mail" },
            Models.SpecialFolder.Drafts  => new[] { "Drafts" },
            Models.SpecialFolder.Sent    => new[] { "Sent", "Sent Items", "Sent Messages" },
            Models.SpecialFolder.Junk    => new[] { "Junk", "Spam", "Junk Email" },
            Models.SpecialFolder.Deleted => new[] { "Trash", "Deleted", "Deleted Items", "Deleted Messages" },
            _ => Array.Empty<string>(),
        };

        var children = await personal.GetSubfoldersAsync(false, ct);
        foreach (var name in candidates)
        {
            var match = children.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }
        return null;
    }

    public async Task<Result<(string PlainText, string Html)>> FetchBodyAsync(
        Account account, string password, string folderFullPath, uint uid,
        CancellationToken ct = default)
    {
        try
        {
            using var client = new ImapClient();
            await ConnectAndAuthenticateAsync(client, account, password, ct);

            var folder = await GetFolderByPathAsync(client, folderFullPath, ct);
            if (folder is null)
            {
                await client.DisconnectAsync(true, ct);
                return Result.Fail<(string, string)>($"Folder not found: {folderFullPath}");
            }

            await folder.OpenAsync(FolderAccess.ReadOnly, ct);
            var msg = await folder.GetMessageAsync(new UniqueId(uid), ct);

            string plain = msg.TextBody ?? "";
            string html  = msg.HtmlBody  ?? "";

            await client.DisconnectAsync(true, ct);
            return Result.Ok((plain, html));
        }
        catch (Exception ex)
        {
            return Result.Fail<(string, string)>(SanitizeError(ex), ex);
        }
    }

    // ─── helpers ──────────────────────────────────────────────────────

    private static async Task ConnectAndAuthenticateAsync(
        ImapClient client, Account account, string password, CancellationToken ct)
    {
        var options = account.ImapEncryption switch
        {
            MailEncryption.SslTls   => SecureSocketOptions.SslOnConnect,
            MailEncryption.StartTls => SecureSocketOptions.StartTls,
            MailEncryption.None     => SecureSocketOptions.None,
            _                       => SecureSocketOptions.Auto,
        };

        await client.ConnectAsync(account.ImapHost, account.ImapPort, options, ct);
        await client.AuthenticateAsync(account.Username, password, ct);
    }

    /// <summary>
    /// Recursively walks the IMAP folder tree, returning a hierarchical list:
    /// each folder's Children populated with its sub-folders. NoSelect parents
    /// are skipped but their children are flattened up to the next selectable
    /// ancestor.
    /// </summary>
    private static async Task CollectFoldersTreeAsync(
        IMailFolder parent, List<Folder> outList, string accountId, bool isNested,
        CancellationToken ct, HashSet<string> seen)
    {
        var children = await parent.GetSubfoldersAsync(false, ct);
        foreach (var c in children)
        {
            if (seen.Contains(c.FullName)) continue;
            if ((c.Attributes & FolderAttributes.NonExistent) != 0) continue;

            // Non-selectable container: skip the node itself, but keep walking
            // its children so they show up under whatever real parent we last had.
            if ((c.Attributes & FolderAttributes.NoSelect) != 0)
            {
                if ((c.Attributes & FolderAttributes.HasChildren) != 0)
                    await CollectFoldersTreeAsync(c, outList, accountId, isNested, ct, seen);
                continue;
            }

            try
            {
                await c.StatusAsync(StatusItems.Count | StatusItems.Unread, ct);
            }
            catch { /* status fetch is best-effort */ }

            var f = new Folder
            {
                Id = c.FullName,
                Name = c.Name,
                AccountId = accountId,
                FullPath = c.FullName,
                Special = MapSpecial(c),
                UnreadCount = c.Unread,
                TotalCount = c.Count,
                IsNested = isNested,
            };
            outList.Add(f);
            seen.Add(c.FullName);

            if ((c.Attributes & FolderAttributes.HasChildren) != 0)
            {
                await CollectFoldersTreeAsync(c, f.Children, accountId, isNested: true, ct, seen);
            }
        }
    }

    private static GreatEmailApp.Core.Models.SpecialFolder MapSpecial(IMailFolder f)
    {
        if ((f.Attributes & FolderAttributes.Inbox)   != 0) return Models.SpecialFolder.Inbox;
        if ((f.Attributes & FolderAttributes.Drafts)  != 0) return Models.SpecialFolder.Drafts;
        if ((f.Attributes & FolderAttributes.Sent)    != 0) return Models.SpecialFolder.Sent;
        if ((f.Attributes & FolderAttributes.Trash)   != 0) return Models.SpecialFolder.Deleted;
        if ((f.Attributes & FolderAttributes.Junk)    != 0) return Models.SpecialFolder.Junk;
        if ((f.Attributes & FolderAttributes.Archive) != 0) return Models.SpecialFolder.Archive;

        // Some servers (Dovecot defaults among them) don't set IMAP \Special-Use flags;
        // fall back to name matching for the obvious cases.
        return f.Name.ToLowerInvariant() switch
        {
            "inbox"           => Models.SpecialFolder.Inbox,
            "drafts"          => Models.SpecialFolder.Drafts,
            "sent" or "sent items" or "sent messages" => Models.SpecialFolder.Sent,
            "trash" or "deleted" or "deleted items"   => Models.SpecialFolder.Deleted,
            "junk" or "spam"  => Models.SpecialFolder.Junk,
            "archive"         => Models.SpecialFolder.Archive,
            _                 => Models.SpecialFolder.None,
        };
    }

    private static async Task<IMailFolder?> GetFolderByPathAsync(ImapClient client, string fullPath, CancellationToken ct)
    {
        try
        {
            return await client.GetFolderAsync(fullPath, ct);
        }
        catch (FolderNotFoundException)
        {
            // Try INBOX as a fallback so the UI can recover from stale paths.
            return client.Inbox;
        }
    }

    private static Message Map(IMessageSummary s, string accountId, string folderPath)
    {
        var env = s.Envelope;
        var from = env?.From?.Mailboxes?.FirstOrDefault();
        var senderName  = from?.Name ?? from?.Address ?? "(unknown)";
        var senderEmail = from?.Address ?? "";

        var initials = MakeInitials(senderName);
        var color = ColorForString(senderEmail.Length > 0 ? senderEmail : senderName);

        var date = s.Date != default ? s.Date.LocalDateTime : DateTime.Now;
        var time = FormatShortTime(date);
        var fullTime = date.ToString("ddd, MMM d, yyyy, h:mm tt");
        var group = GroupFor(date);

        var to = string.Join(", ", env?.To?.Mailboxes?.Select(m => m.Address) ?? Array.Empty<string>());

        bool unread = (s.Flags & MessageFlags.Seen) == 0;
        bool flagged = (s.Flags & MessageFlags.Flagged) != 0;
        bool answered = (s.Flags & MessageFlags.Answered) != 0;

        var msg = new Message
        {
            Id = s.UniqueId.ToString(),
            AccountId = accountId,
            FolderId = folderPath,
            Group = group,
            Sender = senderName,
            SenderEmail = senderEmail,
            Avatar = initials,
            Color = color,
            Subject = env?.Subject ?? "(no subject)",
            Preview = s.PreviewText ?? "",
            Time = time,
            FullTime = fullTime,
            Unread = unread,
            Flagged = flagged,
            Important = false,
            To = to,
            Cc = string.Join(", ", env?.Cc?.Mailboxes?.Select(m => m.Address) ?? Array.Empty<string>()),
        };

        // Mark attachments (only metadata, not the body).
        if (s.Attachments != null)
        {
            foreach (var a in s.Attachments)
            {
                var name = a.FileName ?? "attachment";
                var ext = Path.GetExtension(name).TrimStart('.').ToUpperInvariant();
                var size = FormatSize((long)a.Octets);
                msg.Attachments.Add(new Attachment
                {
                    Name = name,
                    Size = size,
                    Extension = string.IsNullOrEmpty(ext) ? "FILE" : ext,
                    Color = ColorForString(ext),
                });
            }
        }

        return msg;
    }

    private static string FormatShortTime(DateTime d)
    {
        var now = DateTime.Now;
        if (d.Date == now.Date) return d.ToString("h:mm tt");
        if (d.Date == now.Date.AddDays(-1)) return "Yesterday";
        if (d > now.AddDays(-7)) return d.ToString("ddd");
        return d.ToString("MMM d");
    }

    private static string GroupFor(DateTime d)
    {
        var now = DateTime.Now;
        if (d.Date == now.Date) return "Today";
        if (d.Date == now.Date.AddDays(-1)) return "Yesterday";
        if (d > now.AddDays(-7)) return "Last Week";
        if (d > now.AddDays(-30)) return "Earlier This Month";
        return d.ToString("MMMM yyyy");
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:0.#} KB";
        return $"{bytes / (1024.0 * 1024.0):0.#} MB";
    }

    private static string MakeInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(new[] { ' ', '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return name[..Math.Min(2, name.Length)].ToUpperInvariant();
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return (parts[0][0].ToString() + parts[1][0]).ToUpperInvariant();
    }

    // Stable hash → palette. Keeps the same sender always the same color.
    private static readonly string[] Palette = new[]
    {
        "#3A6FF8", "#14a37f", "#8a5cf5", "#d29014",
        "#d4406b", "#0ea5e9", "#f43f5e", "#22c55e",
    };
    private static string ColorForString(string s)
    {
        if (string.IsNullOrEmpty(s)) return Palette[0];
        unchecked
        {
            int h = 23;
            foreach (var c in s) h = h * 31 + c;
            return Palette[Math.Abs(h) % Palette.Length];
        }
    }

    private static string SanitizeError(Exception ex)
    {
        // Strip server-banner noise; never leak credentials in messages.
        // NOTE: rulebook §11 requires user-friendly messages, no stack traces in UI.
        return ex switch
        {
            AuthenticationException => "Authentication failed. Check your username and password.",
            SslHandshakeException   => "Could not establish a secure (TLS) connection.",
            ImapProtocolException   => "The IMAP server returned an unexpected response.",
            System.Net.Sockets.SocketException => "Could not reach the server. Check the host name and port.",
            TimeoutException        => "Connection timed out.",
            OperationCanceledException => "Cancelled.",
            _                       => ex.Message,
        };
    }
}
