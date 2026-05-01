// FILE: src/GreatEmailApp.Core/Search/SqliteMessageCache.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// SQLite-backed message envelope + body cache, with FTS5 for search.
// Schema:
//   messages(account_id, account_email, folder_path, uid, sender, sender_email,
//            subject, preview, body_plain, sent_at, has_attachments, unread,
//            indexed_at, PRIMARY KEY(account_id, folder_path, uid))
//   messages_fts (FTS5 virtual table over sender+subject+preview+body_plain,
//                 with content='messages' for external-content storage)
//
// We use external-content FTS5 + manual sync (INSERT/DELETE on triggers) so
// the row content lives once in `messages` and FTS just indexes it. Saves
// disk and avoids out-of-date FTS rows when envelopes get re-upserted.

using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;
using GreatEmailApp.Core.Storage;
using Microsoft.Data.Sqlite;

namespace GreatEmailApp.Core.Search;

public sealed class SqliteMessageCache : IMessageCache
{
    private readonly string _connStr;
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public SqliteMessageCache()
    {
        AppPaths.EnsureRoot();
        _connStr = new SqliteConnectionStringBuilder
        {
            DataSource = AppPaths.CacheDb,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();
    }

    public async Task InitAsync(CancellationToken ct = default)
    {
        await using var conn = new SqliteConnection(_connStr);
        await conn.OpenAsync(ct).ConfigureAwait(false);

        // WAL keeps reads from blocking writes during indexing.
        await Exec(conn, "PRAGMA journal_mode=WAL;", ct);
        await Exec(conn, "PRAGMA synchronous=NORMAL;", ct);

        await Exec(conn, @"
            CREATE TABLE IF NOT EXISTS messages (
                account_id        TEXT NOT NULL,
                account_email     TEXT NOT NULL,
                folder_path       TEXT NOT NULL,
                uid               INTEGER NOT NULL,
                sender            TEXT NOT NULL DEFAULT '',
                sender_email      TEXT NOT NULL DEFAULT '',
                subject           TEXT NOT NULL DEFAULT '',
                preview           TEXT NOT NULL DEFAULT '',
                body_plain        TEXT NOT NULL DEFAULT '',
                sent_at           TEXT,
                has_attachments   INTEGER NOT NULL DEFAULT 0,
                unread            INTEGER NOT NULL DEFAULT 0,
                indexed_at        TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (account_id, folder_path, uid)
            );", ct);

        await Exec(conn, @"
            CREATE VIRTUAL TABLE IF NOT EXISTS messages_fts USING fts5(
                sender, sender_email, subject, preview, body_plain,
                content='messages',
                content_rowid='rowid',
                tokenize='unicode61'
            );", ct);

        // Manual triggers — keep FTS in lockstep with `messages`.
        await Exec(conn, @"
            CREATE TRIGGER IF NOT EXISTS messages_ai AFTER INSERT ON messages BEGIN
                INSERT INTO messages_fts(rowid, sender, sender_email, subject, preview, body_plain)
                VALUES (new.rowid, new.sender, new.sender_email, new.subject, new.preview, new.body_plain);
            END;", ct);
        await Exec(conn, @"
            CREATE TRIGGER IF NOT EXISTS messages_ad AFTER DELETE ON messages BEGIN
                INSERT INTO messages_fts(messages_fts, rowid, sender, sender_email, subject, preview, body_plain)
                VALUES ('delete', old.rowid, old.sender, old.sender_email, old.subject, old.preview, old.body_plain);
            END;", ct);
        await Exec(conn, @"
            CREATE TRIGGER IF NOT EXISTS messages_au AFTER UPDATE ON messages BEGIN
                INSERT INTO messages_fts(messages_fts, rowid, sender, sender_email, subject, preview, body_plain)
                VALUES ('delete', old.rowid, old.sender, old.sender_email, old.subject, old.preview, old.body_plain);
                INSERT INTO messages_fts(rowid, sender, sender_email, subject, preview, body_plain)
                VALUES (new.rowid, new.sender, new.sender_email, new.subject, new.preview, new.body_plain);
            END;", ct);
    }

    public async Task<Result<bool>> UpsertEnvelopesAsync(
        string accountId, string accountEmail, string folderPath,
        IEnumerable<Message> messages, CancellationToken ct = default)
    {
        await _writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var conn = new SqliteConnection(_connStr);
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct).ConfigureAwait(false);

            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            // ON CONFLICT preserves body_plain — it's expensive to refetch and the
            // envelope upsert path doesn't have it.
            cmd.CommandText = @"
                INSERT INTO messages
                    (account_id, account_email, folder_path, uid, sender, sender_email,
                     subject, preview, sent_at, has_attachments, unread, indexed_at)
                VALUES
                    (@aid, @aemail, @folder, @uid, @sender, @semail,
                     @subject, @preview, @sent, @hasAtt, @unread, CURRENT_TIMESTAMP)
                ON CONFLICT(account_id, folder_path, uid) DO UPDATE SET
                    account_email = excluded.account_email,
                    sender        = excluded.sender,
                    sender_email  = excluded.sender_email,
                    subject       = excluded.subject,
                    preview       = excluded.preview,
                    sent_at       = excluded.sent_at,
                    has_attachments = excluded.has_attachments,
                    unread        = excluded.unread,
                    indexed_at    = CURRENT_TIMESTAMP;";

            var pAid    = cmd.CreateParameter(); pAid.ParameterName    = "@aid";    cmd.Parameters.Add(pAid);
            var pAemail = cmd.CreateParameter(); pAemail.ParameterName = "@aemail"; cmd.Parameters.Add(pAemail);
            var pFolder = cmd.CreateParameter(); pFolder.ParameterName = "@folder"; cmd.Parameters.Add(pFolder);
            var pUid    = cmd.CreateParameter(); pUid.ParameterName    = "@uid";    cmd.Parameters.Add(pUid);
            var pSender = cmd.CreateParameter(); pSender.ParameterName = "@sender"; cmd.Parameters.Add(pSender);
            var pSemail = cmd.CreateParameter(); pSemail.ParameterName = "@semail"; cmd.Parameters.Add(pSemail);
            var pSubj   = cmd.CreateParameter(); pSubj.ParameterName   = "@subject"; cmd.Parameters.Add(pSubj);
            var pPrev   = cmd.CreateParameter(); pPrev.ParameterName   = "@preview"; cmd.Parameters.Add(pPrev);
            var pSent   = cmd.CreateParameter(); pSent.ParameterName   = "@sent";   cmd.Parameters.Add(pSent);
            var pAtt    = cmd.CreateParameter(); pAtt.ParameterName    = "@hasAtt"; cmd.Parameters.Add(pAtt);
            var pUn     = cmd.CreateParameter(); pUn.ParameterName     = "@unread"; cmd.Parameters.Add(pUn);

            foreach (var m in messages)
            {
                if (!uint.TryParse(m.Id, out var uid)) continue;
                pAid.Value    = accountId;
                pAemail.Value = accountEmail;
                pFolder.Value = folderPath;
                pUid.Value    = uid;
                pSender.Value = m.Sender ?? "";
                pSemail.Value = m.SenderEmail ?? "";
                pSubj.Value   = m.Subject ?? "";
                pPrev.Value   = m.Preview ?? "";
                pSent.Value   = (object?)m.FullTime ?? DBNull.Value;  // Time is short, FullTime is the long form
                pAtt.Value    = (m.Attachments?.Count ?? 0) > 0 ? 1 : 0;
                pUn.Value     = m.Unread ? 1 : 0;
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await tx.CommitAsync(ct).ConfigureAwait(false);
            return Result.Ok(true);
        }
        catch (Exception ex)
        {
            return Result.Fail<bool>($"UpsertEnvelopes failed: {ex.Message}", ex);
        }
        finally { _writeGate.Release(); }
    }

    public async Task<Result<bool>> UpsertBodyAsync(
        string accountId, string folderPath, uint uid, string bodyPlain,
        CancellationToken ct = default)
    {
        await _writeGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var conn = new SqliteConnection(_connStr);
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE messages SET body_plain = @body
                WHERE account_id = @aid AND folder_path = @folder AND uid = @uid;";
            cmd.Parameters.AddWithValue("@body", bodyPlain ?? "");
            cmd.Parameters.AddWithValue("@aid", accountId);
            cmd.Parameters.AddWithValue("@folder", folderPath);
            cmd.Parameters.AddWithValue("@uid", uid);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            return Result.Ok(true);
        }
        catch (Exception ex)
        {
            return Result.Fail<bool>($"UpsertBody failed: {ex.Message}", ex);
        }
        finally { _writeGate.Release(); }
    }

    public async Task<Result<List<SearchHit>>> SearchAsync(
        string query, int limit = 30, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query)) return Result.Ok(new List<SearchHit>());
            var fts = BuildFtsQuery(query);
            if (string.IsNullOrEmpty(fts)) return Result.Ok(new List<SearchHit>());

            await using var conn = new SqliteConnection(_connStr);
            await conn.OpenAsync(ct).ConfigureAwait(false);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT m.account_id, m.account_email, m.folder_path, m.uid,
                       m.sender, m.sender_email, m.subject, m.preview, m.sent_at,
                       snippet(messages_fts, 4, '[', ']', '…', 12) AS snip
                FROM messages_fts
                JOIN messages m ON m.rowid = messages_fts.rowid
                WHERE messages_fts MATCH @q
                ORDER BY bm25(messages_fts), m.sent_at DESC
                LIMIT @limit;";
            cmd.Parameters.AddWithValue("@q", fts);
            cmd.Parameters.AddWithValue("@limit", limit);

            var hits = new List<SearchHit>();
            await using var rdr = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await rdr.ReadAsync(ct).ConfigureAwait(false))
            {
                DateTimeOffset? sentAt = null;
                if (!rdr.IsDBNull(8))
                {
                    var s = rdr.GetString(8);
                    if (DateTimeOffset.TryParse(s, out var dt)) sentAt = dt;
                }
                hits.Add(new SearchHit(
                    AccountId:    rdr.GetString(0),
                    AccountEmail: rdr.GetString(1),
                    FolderPath:   rdr.GetString(2),
                    Uid:          (uint)rdr.GetInt64(3),
                    Sender:       rdr.GetString(4),
                    SenderEmail:  rdr.GetString(5),
                    Subject:      rdr.GetString(6),
                    Snippet:      rdr.IsDBNull(9) ? rdr.GetString(7) : rdr.GetString(9),
                    SentAt:       sentAt));
            }
            return Result.Ok(hits);
        }
        catch (Exception ex)
        {
            return Result.Fail<List<SearchHit>>($"Search failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Translate a free-form user query to FTS5 syntax. Splits on whitespace,
    /// drops anything that looks like FTS5 punctuation, ANDs the rest with a
    /// trailing prefix wildcard for type-as-you-go matching.
    /// </summary>
    private static string BuildFtsQuery(string query)
    {
        var tokens = query
            .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => new string(t.Where(c => char.IsLetterOrDigit(c) || c == '@' || c == '-' || c == '.').ToArray()))
            .Where(t => t.Length > 0)
            .Select(t => t + "*")
            .ToArray();
        return string.Join(" AND ", tokens);
    }

    private static async Task Exec(SqliteConnection conn, string sql, CancellationToken ct)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
