// FILE: src/GreatEmailApp.Core/Services/IImapService.cs
// Created: 2026-04-29 | Revised: 2026-11-21 | Rev: 4
// Changed by: Claude Opus 5.5 on behalf of James Reed

using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Services;

/// <summary>Live IMAP/SMTP operations. All async, all returning Result&lt;T&gt;.</summary>
public interface IImapService
{
    /// <summary>Open a connection and authenticate. Used by Test Connection in Add Account dialog.</summary>
    Task<Result<bool>> TestConnectionAsync(Account account, string password, CancellationToken ct = default);

    /// <summary>Top-level folder list for the account, with unread/total counts.</summary>
    Task<Result<List<Folder>>> ListFoldersAsync(Account account, string password, CancellationToken ct = default);

    /// <summary>
    /// Fetch the most recent <paramref name="limit"/> messages in <paramref name="folderFullPath"/>.
    /// Headers + preview only — body comes via <see cref="FetchBodyAsync"/>.
    /// </summary>
    Task<Result<List<Message>>> ListMessagesAsync(
        Account account, string password, string folderFullPath,
        int limit = 200, CancellationToken ct = default);

    /// <summary>Fetch the full body for a single message UID.</summary>
    Task<Result<(string PlainText, string Html)>> FetchBodyAsync(
        Account account, string password, string folderFullPath, uint uid,
        CancellationToken ct = default);

    /// <summary>Add or remove the \Seen flag.</summary>
    Task<Result<bool>> SetSeenAsync(Account account, string password,
        string folderFullPath, uint uid, bool seen, CancellationToken ct = default);

    /// <summary>Add or remove the \Flagged flag.</summary>
    Task<Result<bool>> SetFlaggedAsync(Account account, string password,
        string folderFullPath, uint uid, bool flagged, CancellationToken ct = default);

    /// <summary>
    /// Download a message's attachments into <paramref name="destDir"/> (created
    /// if missing) and return the saved file paths. Used by Forward so the
    /// original attachments go along with the forwarded message.
    /// </summary>
    Task<Result<List<string>>> SaveAttachmentsAsync(Account account, string password,
        string folderFullPath, uint uid, string destDir, CancellationToken ct = default);

    /// <summary>Add or remove a <see cref="MailMark"/> (\Flagged or the Priority keyword).</summary>
    Task<Result<bool>> SetMarkAsync(Account account, string password,
        string folderFullPath, uint uid, MailMark mark, bool on, CancellationToken ct = default);

    /// <summary>
    /// Add or remove a mark on many messages over ONE connection (grouped by
    /// folder). Returns how many messages were changed. Backs "Unflag all" /
    /// "Clear all priority".
    /// </summary>
    Task<Result<int>> SetMarkBulkAsync(Account account, string password,
        IReadOnlyList<(string FolderFullPath, uint Uid)> messages, MailMark mark, bool on,
        CancellationToken ct = default);

    /// <summary>Move a message to a destination folder by full path.</summary>
    Task<Result<bool>> MoveToFolderAsync(Account account, string password,
        string srcFolderFullPath, uint uid, string dstFolderFullPath,
        CancellationToken ct = default);

    /// <summary>Move a message to the account's special-use folder
    /// (Archive / Trash / Junk). Returns the destination path actually used.</summary>
    Task<Result<string>> MoveToSpecialAsync(Account account, string password,
        string srcFolderFullPath, uint uid, SpecialFolder dst,
        CancellationToken ct = default);

    /// <summary>
    /// Permanently delete one message: mark it \Deleted and expunge it in place.
    /// It never goes to Trash and cannot be recovered. Expunges only this UID
    /// (UID EXPUNGE / UIDPLUS) so other messages already flagged \Deleted by
    /// another client are left alone.
    /// </summary>
    Task<Result<bool>> DeletePermanentlyAsync(Account account, string password,
        string folderFullPath, uint uid, CancellationToken ct = default);

    /// <summary>
    /// Move a message to a folder named by its path from the account root
    /// (e.g. ["All Spam", "TGEA"]) — delimiter- and namespace-agnostic, so the
    /// same setting works on every account. Missing folders along the path are
    /// created (and subscribed) when <paramref name="createIfMissing"/> is true.
    /// Returns the destination's full IMAP path.
    /// </summary>
    Task<Result<string>> MoveToNamedPathAsync(Account account, string password,
        string srcFolderFullPath, uint uid, IReadOnlyList<string> pathSegments,
        bool createIfMissing, CancellationToken ct = default);

    /// <summary>
    /// Append a sent copy to the account's Sent folder. SMTP doesn't do this for us.
    /// Best-effort — failure here doesn't fail the send (the message already left).
    /// </summary>
    Task<Result<bool>> AppendToSentAsync(Account account, string password,
        MimeKit.MimeMessage message, CancellationToken ct = default);

    /// <summary>
    /// Create a new folder. Pass empty <paramref name="parentFullPath"/> to create
    /// at the root of the account's personal namespace; otherwise the new folder
    /// is created as a child of that path. Returns the full path of the new folder.
    /// </summary>
    Task<Result<string>> CreateFolderAsync(Account account, string password,
        string parentFullPath, string name, CancellationToken ct = default);

    /// <summary>Rename a folder in place — keeps the same parent, changes the leaf name.
    /// Returns the new full path.</summary>
    Task<Result<string>> RenameFolderAsync(Account account, string password,
        string folderFullPath, string newName, CancellationToken ct = default);

    /// <summary>Delete a folder. Server semantics decide whether non-empty folders
    /// or folders with subfolders are refused — surfaced as a Fail result.</summary>
    Task<Result<bool>> DeleteFolderAsync(Account account, string password,
        string folderFullPath, CancellationToken ct = default);

    /// <summary>Mark every message in the folder \Deleted and expunge. The folder
    /// itself remains. Returns the count of messages removed.</summary>
    Task<Result<int>> EmptyFolderAsync(Account account, string password,
        string folderFullPath, CancellationToken ct = default);

    /// <summary>
    /// Search every selectable folder in the account for messages whose From
    /// header OR subject contains <paramref name="query"/>. Returns at most
    /// <paramref name="limit"/> results, ordered newest-first by envelope
    /// date. Used by the "Search server for more results" link in the mail
    /// list — the local in-memory filter only sees the latest 200 of the
    /// current folder, so anything older or in another folder needs this.
    /// </summary>
    Task<Result<List<Message>>> SearchAccountAsync(Account account, string password,
        string query, int limit = 200, CancellationToken ct = default);

    /// <summary>
    /// Every message carrying <paramref name="mark"/> in every selectable folder
    /// of the account (Trash and Junk skipped), newest first, at most
    /// <paramref name="limit"/>. Backs the sidebar's "Flagged" and "Priority"
    /// views, which show marks from all folders and accounts at once.
    /// </summary>
    Task<Result<List<Message>>> SearchMarkedAsync(Account account, string password,
        MailMark mark, int limit = 500, CancellationToken ct = default);
}
