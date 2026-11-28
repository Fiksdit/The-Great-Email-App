# Fix Log

Permanent record of bug-class changes per rulebook §16. Newest first.

---

## FIX-2026-11-27-006 — Reading-pane Forward/Reply did nothing; Forward dropped attachments; added attachments invisible

**Area:** UI / ReadingPane, ComposeWindow · Core / ImapService.SaveAttachmentsAsync (new)
**Status:** ✅ Fixed
**Priority:** P1 (a forward to Pam went out without Travis's attachments)

**Symptom**
- Forward / Reply / Reply all in the reading pane did nothing. The ribbon and right-click versions worked.
- A forwarded message arrived without the original's attachments.
- Files added with Attach never appeared in the compose window (they were still sent).

**Replicate**
1. Open a message, click Forward above the message body → nothing happens.
2. Forward from the ribbon a message with attachments → the attachment list is empty.

**Root cause**
- `ReadingPane.OpenCompose` checked the UserControl's DataContext (MainViewModel). The buttons sit in a DockPanel bound to SelectedMessage, so the check always failed. Broken since the compose commit (98da3d7).
- `PrepareForward` only quoted the body; nothing downloaded the original's attachments.
- The attachment strip's Visibility bound to the collection object, which never raises a change when items are added.

**Tried**
- Nothing. Each was found by reproducing in the app.

**Fix**
- ReadingPane reads the clicked button's DataContext.
- New `IImapService.SaveAttachmentsAsync` downloads MIME attachments (attached .eml included) to `%LOCALAPPDATA%\GreatEmailApp\forwarded\<guid>` (folders older than 30 days pruned). Forward adds them as normal removable attachments. Send stays disabled until they're in (`IsLoadingAttachments`).
- The attachment strip binds `HasAttachments`, refreshed on CollectionChanged.
- Verified: reading-pane Forward on Travis's "Re: Petfolk docs" showed all 5 attachments (3 PDFs, PNG, XLSX); discarded without sending.

**Files changed**
- `src/GreatEmailApp/Controls/ReadingPane.xaml.cs` (Rev 3)
- `src/GreatEmailApp.Core/Services/IImapService.cs`, `ImapService.cs`
- `src/GreatEmailApp/ViewModels/ComposeViewModel.cs`, `Views/ComposeWindow.xaml(.cs)`

**Rulebook**
- Buttons inside a sub-tree bound to a different DataContext must read `(sender as FrameworkElement).DataContext`, not the UserControl's.

**Session:** 2026-11-27
**Commit:** _pending_

---

## FIX-2026-11-27-005 — Send asked "save as draft / discard" after sending; Forward didn't focus To; empty address book

**Area:** UI / ComposeWindow, ComposeViewModel, AddressInput · data / contacts.json
**Status:** ✅ Fixed (Send close path not exercised with a real send)
**Priority:** P1 (looked like Send was broken)

**Symptom**
- Clicking Send showed only "Save this message as a draft? / discard". The forward had in fact been sent (INBOX.Sent Messages, 14:17, to preed@).
- Forward opened with the caret in the body, not To.
- No address suggestions: contacts.json was empty.

**Replicate**
1. Forward a message, add a recipient, click Send → the draft prompt appears.

**Root cause**
- After a successful send, `Sent` → `Close()` → `OnClosing`. `HasUnsavedChanges` was still true, so the close prompt ran as if unsent.
- No initial focus was set; the WebView2 editor took it.
- Contacts only auto-collect on send from this app, which had effectively never sent.

**Tried**
- Confirmed via the Sent folder that the send succeeded before changing anything.

**Fix**
- Send success clears `HasUnsavedChanges`, and the window closes through `CloseForReal` (no prompt).
- New and Forward focus the To box after render (`AddressInput.FocusBox`); Reply keeps body focus.
- Seeded the address book with 962 contacts (AutoCollected) from every To/Cc in all Sent folders: jreed@ (4,063 messages), james@fiksdit.com (113). Backup: scratchpad contacts-backup-2026-11-27.json. Synced 20:24Z.
- Verified: Forward and New open with typing landing in To; "mich" suggests Michael Dixon, Michelle Hanson, etc.

**Files changed**
- `src/GreatEmailApp/Views/ComposeWindow.xaml.cs` (Rev 3), `ViewModels/ComposeViewModel.cs` (Rev 2), `Controls/AddressInput.xaml.cs` (Rev 2)

**Rulebook**
- none. Watch: contacts.json is now ~300 KB inside the synced settings doc (Firestore's limit is 1 MB per doc).

**Session:** 2026-11-27
**Commit:** _pending_

---

## FIX-2026-11-27-004 — Flagged / Priority views waited 10–15 s for a full search every time

**Area:** Core / MarkedMailCache (new) · UI / MainViewModel marked views
**Status:** ✅ Fixed
**Priority:** P2

**Symptom**
- Every open of Flagged or Priority re-ran an IMAP search of ~150 folders before anything showed.

**Replicate**
1. Click Flagged in the sidebar: the list is empty for 10–15 s.

**Root cause**
- No memory of previous results; the search was the only source.

**Tried**
- Nothing. Built the cache on the first attempt. One test showed "Searching…" instead of the cached list. Two app instances were running at the time (the user had opened a second copy); not reproduced with a single instance.

**Fix**
- `MarkedMailCache`: per-account Flagged / Priority lists in memory plus `marked-mail.json` (local-only, bodies stripped). Views paint from it instantly, then `RefreshAsync` re-checks the server and re-renders only if something changed, keeping the open message and its body.
- Background refreshes: 20 s after startup and every 15 min (marks changed in Outlook, webmail or the other PC); 10 s after rules flag or mark new arrivals (`RulesEngine.MarksApplied`, only new UIDs or a manual run); 5 s after a marked message is moved or deleted.
- Local flag/priority toggles patch the cache at once. Edits made during a refresh (10 s margin) are replayed over its result, so an in-flight search can't undo a click.
- Verified: fresh start, Priority (9) and Flagged (17) appeared in under 1 s with "checking the server for changes…".

**Files changed**
- `src/GreatEmailApp.Core/Services/MarkedMailCache.cs` (new), `Storage/AppPaths.cs`
- `src/GreatEmailApp.Core/Rules/IRulesEngine.cs`, `RulesEngine.cs`
- `src/GreatEmailApp/App.xaml.cs`, `ViewModels/MainViewModel.cs`

**Rulebook**
- none

**Session:** 2026-11-27
**Commit:** _pending_

---

## FIX-2026-11-27-003 — Flag clicks slow and sometimes ignored; rule flags mixed with the user's own review flags

**Area:** UI / MainViewModel mark commands · Core / IMAP marks, RulesEngine, MailRule
**Status:** ✅ Fixed
**Priority:** P2

**Symptom**
- Clicking the flag reacted slowly. A second click within ~2 s (another row, or the menu) did nothing.
- Rules flagged Federated mail with the same flag the user uses for "review later", so clearing rule flags would also wipe the user's own.

**Replicate**
1. Click a row's flag, then quickly click another row's flag: the second click is dropped.

**Root cause**
- `ToggleFlagAsync` waited for a fresh IMAP connect + STORE (1–2 s) before changing the icon.
- CommunityToolkit `[RelayCommand]` async commands refuse to run again while the previous run is still in progress, so clicks during that window were silently ignored.

**Tried**
- What looked like a second bug (icon not reappearing after re-marking from the menu) was a screenshot taken before the closing menu redrew. Confirmed with a trace that the same row object was updated and the server got the change.

**Fix**
- Optimistic toggle: the icon flips immediately and reverts with a status message if the server refuses. Mark commands allow concurrent runs. Server writes are serialized through `_markGate` so they land in click order.
- New **Priority** mark: IMAP keyword `Priority` (`MailMarks`), separate from \Flagged. Rule action "Mark priority (alert)" is stored as Flag + Value "priority" (sync-compat: older clients degrade to a plain Flag). A new arrival marked by such a rule raises `PriorityMailArrived` → its own tray alert, shown even when ordinary new-mail notifications are off.
- Sidebar "Priority · all folders" view, an amber "!" on rows (click to clear), and a "Mark / Clear priority" menu item. "Unflag all" / "Clear all priority" button in the marked views, with a confirmation and one connection per account (`SetMarkBulkAsync`).
- Migration: the 4 rule Flag actions (3 Federated + Missouri DOR) switched to Mark priority; 9 November Federated messages moved from \Flagged to Priority. Older DOR flags (2014, June 2026) left alone as the user's own. Backup: scratchpad rules-backup-2026-11-27.json.
- Verified: two rows unflagged then re-flagged in quick succession, each instant; menu clear/mark checked against the server; Priority view lists 9; Clear-all confirmation, then Cancel, changed nothing. The priority tray alert was not tested live (it needs new Federated mail to arrive).

**Files changed**
- `src/GreatEmailApp.Core/Models/MailMarks.cs` (new), `Message.cs`, `MailRule.cs` (Rev 4)
- `src/GreatEmailApp.Core/Services/IImapService.cs`, `ImapService.cs`
- `src/GreatEmailApp.Core/Rules/IRulesEngine.cs`, `RulesEngine.cs` (Rev 4)
- `src/GreatEmailApp/ViewModels/MainViewModel.cs`, `MessageViewModel.cs` (Rev 2)
- `src/GreatEmailApp/Controls/MailList.xaml(.cs)`, `Sidebar.xaml(.cs)`
- `src/GreatEmailApp/Services/TrayNotifier.cs` (Rev 6), `App.xaml.cs`
- `src/GreatEmailApp/Views/Dialogs/RuleEditorDialog.xaml.cs`, `Converters/Converters.cs`

**Rulebook**
- Per-row toggle commands that hit IMAP must be optimistic and use `AllowConcurrentExecutions = true`, or fast clicks get dropped.

**Session:** 2026-11-27
**Commit:** _pending_

---

## FIX-2026-11-27-002 — Sync sign-in lost after a restart; empty Flagged list gave no explanation

**Area:** Core / FirebaseAuthService · UI / MailList empty state
**Status:** ✅ Fixed (sign-in cause inferred, not reproduced)
**Priority:** P1 (losing sign-in means re-doing Google sign-in; sync stops until then)

**Symptom**
- After closing and reopening the app, the user had to sign in to Google again and unlock passwords.
- "Flagged" showed nothing.

**Replicate**
- Not reproduced: two later restarts restored the sign-in silently. The code path below causes it whenever Google can't be reached at the moment of the check.
- Flagged (reproduced): click sidebar "Flagged · all folders" right after startup. The startup folder load finishes mid-search and clears IsBusy, so the list read "No flagged messages" (previously just blank) for the 10–15 s before results arrived. A late folder load could also auto-select INBOX over the Flagged view.

**Root cause**
- `TryRestoreAsync` deleted auth.dat on ANY failed refresh, including 5xx and malformed replies. A network exception threw out of it, so startup gave up, leaving the user signed out until they signed in again.
- `GetValidIdTokenAsync` signed the user out on any failed hourly refresh.
- At the time, the status bar also showed "Could not reach the server" for another account, which points to a shaky network.

**Tried**
- Checked file times: vault.dat unchanged since 11/21; auth.dat kept. Two relaunches restored fine.

**Fix**
- Only 400/401/403, or a lookup with no user, count as a real rejection and clear the saved sign-in. Network errors, timeouts and 5xx are retried at startup (0/3/10/30/60 s) and never delete auth.dat. The hourly refresh no longer signs out on those.
- Mail list shows an explanation when empty. In the Flagged view: "No flagged messages in any folder", or which account couldn't be searched or has no saved password. On a folder's Flagged pill: "No flagged messages in this folder" plus a "Show flagged from all folders" link.
- Flagged view has its own loading flag (not IsBusy) and shows "Searching every folder for flagged messages…" until results arrive. LoadFoldersAsync no longer auto-selects INBOX while the Flagged view is open.
- Verified: relaunch restored sign-in (auth.dat rewritten); empty Inbox Flagged pill showed the text and link; the link opened 29 flagged. Clicking Flagged 4 s after launch showed "Searching…" at 2 s and 6 s, then 29 messages, staying on the Flagged view.

**Files changed**
- `src/GreatEmailApp.Core/Auth/FirebaseAuthService.cs` (Rev 2)
- `src/GreatEmailApp/ViewModels/MainViewModel.cs` (Rev 17)
- `src/GreatEmailApp/Controls/MailList.xaml`

**Rulebook**
- Only a definite server rejection may delete a saved credential or token. Network or 5xx failures retry and keep it.

**Session:** 2026-11-27
**Commit:** _pending_

---

## FIX-2026-11-27-001 — Clicking the flag in the message list did nothing; no way to see all flagged mail

**Area:** UI / MailList · Sidebar · MainViewModel; Core / ImapService search
**Status:** ✅ Fixed
**Priority:** P2 (flagged priority mail, e.g. Federated, is the user's main triage list)

**Symptom**
- The flag icon in a list row was a plain TextBlock, so clicking it did nothing. Unflagging needed the right-click menu.
- The "Flagged" pill only filters the current folder. Rule-flagged mail is filed into many folders, so there was no single place to see it.

**Replicate**
1. Click the red flag on a flagged row: nothing happens.
2. Pick the Flagged pill in INBOX: Federated mail filed to subfolders doesn't show.

**Root cause**
- The row icon had no click handling. There was no cross-folder flagged view.

**Tried**
- Nothing. Fixed on the first attempt.

**Fix**
- The row flag is now a Button: a filled flag when flagged, an outline flag on row hover when not; one click toggles (server first, then icon).
- New sidebar "Flagged · all folders" entry: IMAP SEARCH FLAGGED in every selectable folder of every account (Trash/Junk skipped), accounts in parallel, newest first. Ribbon Refresh reloads it; picking a folder leaves it.
- SearchAccountAsync and SearchFlaggedAsync share one SearchAllFoldersAsync loop.
- Verified in the app: 29 flagged messages across folders; unflag/re-flag from the list worked.

**Files changed**
- `src/GreatEmailApp.Core/Services/IImapService.cs`, `ImapService.cs` (Rev 7)
- `src/GreatEmailApp/ViewModels/MainViewModel.cs` (Rev 17)
- `src/GreatEmailApp/Controls/MailList.xaml`, `MailList.xaml.cs` (Rev 11)
- `src/GreatEmailApp/Controls/Sidebar.xaml`, `Sidebar.xaml.cs` (Rev 6)

**Rulebook**
- none

**Session:** 2026-11-27
**Commit:** _pending_

---

## FIX-2026-11-25-003 — Rules, spam settings and contacts saved while the app was closed never synced

**Area:** Core / SyncMetadata · HasUnpushedLocalChanges
**Status:** ✅ Fixed
**Priority:** P1 (changes silently stay on one PC)

**Symptom**
- Five rules added to rules.json (app closed) were still missing from the cloud after relaunch. sync-meta LastSyncedAt stayed at 2026-11-22.

**Replicate**
1. Change rules.json, spam-filter.json or contacts.json while the app is closed, or have the debounced push fail.
2. Start the app: startup pull sees the cloud UpdatedAt ≤ LastSyncedAt, so it counts as "already applied" and returns. No push.

**Root cause**
- `NewestLocalDataMtime` only checked accounts.json and settings.json (written before rules, spam and contacts sync existed). Newer files in the other synced stores never counted as unpushed.

**Tried**
- Read-only pull with the app's own sign-in: auth and Firestore were fine; the cloud just had 1 rule.

**Fix**
- Count contacts.json, rules.json, rules-deleted.json and spam-filter.json too. Verified: on relaunch the app pushed; cloud now lists all 6 rules (2026-11-25 21:35Z).

**Files changed**
- `src/GreatEmailApp.Core/Sync/SyncMetadata.cs` (Rev 3)

**Rulebook**
- A new synced store must be added to SyncMetadata's mtime list, as well as to the snapshot and the updateMask.

**Session:** 2026-11-25
**Commit:** _pending_

---

## FIX-2026-11-25-002 — Folders created in the app didn't appear in Outlook or webmail

**Area:** Core / ImapService · CreateFolderAsync, RenameFolderAsync, DeleteFolderAsync, MoveToNamedPathAsync
**Status:** ✅ Fixed
**Priority:** P2

**Symptom**
- A folder made in the app showed in every copy of the app, but not in Outlook or the host's webmail.

**Replicate**
1. Right-click a folder → New subfolder.
2. Open Outlook or webmail: the folder is missing.

**Root cause**
- The folder WAS created on the server (the app lists all folders). Outlook and webmail list only SUBSCRIBED folders, and `CreateFolderAsync` never sent SUBSCRIBE. Read-only check found INBOX.All Spam.TGEA, INBOX.test and Credit Cards.Capital One on jreed@ unsubscribed.

**Tried**
- Nothing — LIST vs LSUB comparison showed it directly.

**Fix**
- Create and rename now subscribe the folder; rename and delete unsubscribe the old name. Moves into the spam destination re-subscribe it on every move, so it self-heals. Best-effort helpers that log and never fail the operation.
- One-off: subscribed the 3 folders on the server 2026-11-25. Left james@fixlogic.co INBOX.Junk unsubscribed (server folder, likely deliberate).

**Files changed**
- `src/GreatEmailApp.Core/Services/ImapService.cs` (Rev 6)

**Rulebook**
- Every IMAP folder create/rename must SUBSCRIBE, or other clients won't show it.

**Session:** 2026-11-25
**Commit:** _pending_

---

## FIX-2026-11-25-001 — Learning spam filter flagged a client's reply; would learn good mail from "All Spam" as spam

**Area:** Core / SpamFilterEngine · learning layer and training folder selection
**Status:** ✅ Fixed
**Priority:** P1 (good work mail sent to spam)

**Symptom**
- Owner saw ~100 good emails (Amazon orders, Federated Service work orders and ACH notice, FedEx) in `INBOX.All Spam` and suspected our filter.
- Separately, the learning filter scored a reply from cwalentowski@federatedservice.com ("Re: Urgent WO226682 petfolk") at 27.3, over the 16 cutoff.

**Replicate**
1. A message from a not-yet-trusted address at a company the owner corresponds with, with conversational wording ("told", "going", "just", "they").
2. Learning score > 16 → moved to All Spam/TGEA.

**Root cause**
- All Spam mail itself was NOT ours: our catches go to TGEA, marked read, within seconds. The All Spam mail was 95/108 unread, and its IMAP SAVEDATE was minutes to an hour after arrival, in batches. Another mail program's rule moved it there. The owner fixed that rule on 2026-11-25.
- Learning layer: everyday words lean spam in the owner's mail, and the sender-domain token (104 good / 1 spam) adds only about −3, so wording outweighed a well-known domain.
- Training took spam examples from every folder whose path contains "spam", including the container `All Spam`. That folder now held ~100 good messages, so a Retrain would have taught the model that Amazon and clients are spam.

**Tried**
- Read-only scoring of every All Spam message with the current filter: only 3 of 108 flagged, which proved the bulk came from elsewhere. Header check showed no server X-Spam headers; SAVEDATE vs INTERNALDATE identified client-side moves.

**Fix**
- `SpamFilterEngine.IsKnownCorrespondentDomain`: the learning layer skips senders whose domain has a trusted address, or ≥10 filed messages with good ≥ 10× spam. Never applies to own domains or shared providers (gmail, outlook, etc.). Keyword filter and blocked list still apply.
- `TrainingKind`: a spam-named folder that has subfolders (and isn't the server's \Junk) is skipped as a training source.
- Checked read-only on the live mailbox: All Spam learned flags 2 → 1 (Federated reply cleared); TGEA, Mixed, Specific Domain, Subject, Body Filter and server spam folders unchanged (0 real spam lost).

**Files changed**
- `src/GreatEmailApp.Core/Spam/SpamFilterEngine.cs` (Rev 5)
- `src/GreatEmailApp.Core/Spam/SpamLearningModel.cs` (Rev 2): `DomainCounts`

**Rulebook**
- To tell who moved mail on Dovecot, compare SAVEDATE with INTERNALDATE. Delivery-time filters save at arrival; client moves save later.

**Session:** 2026-11-25
**Commit:** _pending_

---

## FIX-2026-11-21-005 — Pasting into a rule condition could crash the app

**Area:** UI / RuleEditorDialog · condition value paste handler
**Status:** ✅ Fixed
**Priority:** P1 (crash, unsaved rule lost)

**Symptom**
- The app closed while text was being pasted into a rule's condition box.

**Replicate**
1. Open a rule, click the condition value box.
2. Paste at a moment another program holds the clipboard open (clipboard managers, remote-desktop tools and automation do this briefly).

**Root cause**
- The paste handler added on 2026-11-21 (joins pasted lines with commas) called `DataObject.GetData` with no error handling. It threw `COMException 0x800401D0 CLIPBRD_E_CANT_OPEN`, which was unhandled on the UI thread and ended the process (Event Viewer: .NET Runtime, 6:00 PM).

**Tried**
- Nothing — stack trace pointed at the line; fixed on first attempt.

**Fix**
- Wrap the read in try/catch for `COMException`; on failure the normal paste runs unchanged.

**Files changed**
- `src/GreatEmailApp/Views/Dialogs/RuleEditorDialog.xaml.cs` (Rev 3)

**Rulebook**
- Any clipboard read must tolerate the clipboard being locked.

**Session:** 2026-11-21
**Commit:** _pending_

---

## FIX-2026-11-21-004 — A PC running an older version wiped every rule on the other PCs

**Area:** Core / SyncCoordinator · rules sync
**Status:** ✅ Fixed (for PCs on this version or later)
**Priority:** P0 (silent data loss — a permanent-delete spam rule vanished)

**Symptom**
- Settings → Rules showed "No rules yet". `rules.json` was `[]`, rewritten at 17:47 together with accounts.json and contacts.json (a sync pull), right after the owner opened the app on another PC.

**Replicate**
1. PC A creates a rule; it syncs to the cloud.
2. PC B (v0.12.5, never had the rule) saves any setting, or launches with local files newer than its last sync → it pushes its whole snapshot, including its empty rule list.
3. PC A pulls → rule list replaced with `[]`.

**Root cause**
- Sync is last-write-wins for the whole document, and the rules list was replaced wholesale on pull. A PC that simply hadn't received a rule yet was indistinguishable from one that deleted it.

**Tried**
- Nothing — diagnosed from file mtimes + v0.12.5's FirestoreSyncService (it does push `rules_json`); fixed on first attempt.

**Fix**
- Rules now MERGE by rule id on both pull and push (`SyncCoordinator.MergeRules`): union of both sides, newer `UpdatedAt` wins, a rule missing on one side is kept.
- Deletions are recorded (`rules-deleted.json`, synced as a new `rules_deleted_json` field that older clients don't touch) so a deleted rule stays deleted.
- Push pulls and merges first; a pull that finds local rules the cloud lacks pushes the merged list back, so the cloud self-heals after an old client overwrites it.
- Rule restored by re-creating it in the rule editor (same 36 extensions, delete permanently, all accounts).

**Known limit**
- Until every PC runs this version, a v0.12.5 PC can still upload an empty list. Newer PCs restore it on their next pull. A rule deleted ON a v0.12.5 PC won't stay deleted. Fix: update the other PCs (cut a release).

**Files changed**
- `src/GreatEmailApp.Core/Sync/SyncCoordinator.cs` (Rev 8), `SyncSnapshot.cs` (Rev 3), `FirestoreSyncService.cs` (Rev 4)
- `src/GreatEmailApp.Core/Services/IRulesStore.cs`, `JsonRulesStore.cs` (Rev 2)
- `src/GreatEmailApp.Core/Storage/AppPaths.cs` (Rev 7)

**Rulebook**
- Same family as FIX-2026-04-30-002: "absent on the other side" must never be read as "deleted". Lists of user-created items need per-item merge, not whole-list replacement.

**Session:** 2026-11-21
**Commit:** _pending_

---

## FIX-2026-11-21-003 — Spam filter's trusted-senders list stayed empty forever

**Area:** Core / SpamFilterEngine · trusted-from-Sent bootstrap
**Status:** ✅ Fixed
**Priority:** P1 (no protection against false positives for anyone the owner has emailed)

**Symptom**
- Settings → Spam → trusted senders: 0 entries, yet `spam-filter.json` had `TrustedBootstrapDone: true`. The owner has 4,000+ sent messages.

**Replicate**
1. New PC: sign in, accounts sync down, passwords not yet restored (vault still locked).
2. App starts → bootstrap runs → every account has no credentials → nothing scanned.
3. List saved empty, `TrustedBootstrapDone = true`; never runs again. The empty list then syncs to every PC.

**Root cause**
- `BootstrapTrustedSendersAsync` `continue`d past accounts with no password / failed IMAP calls, then unconditionally set `TrustedBootstrapDone = true` ("save even if added=0"). It only ran once, at `Start()`, which on a new PC is always before the vault unlock.
- Secondary: it saved the config object it loaded at the start of a multi-second scan (would clobber concurrent edits), and it trusted the user's own addresses (spoofed self-sent phishing would bypass the filter).

**Tried**
- Nothing — diagnosed from the config file + code; fixed on first attempt.

**Fix**
- Done is set only when every account was actually scanned; partial results are still merged and saved.
- Retries on every poll until complete (Interlocked guard; accounts already scanned this session are skipped).
- `NeedsBootstrap`: "done but empty" counts as not done → existing broken installs self-heal.
- Re-loads config right before save and unions into it.
- Excludes the user's own account addresses.
- Verified: list went 0 → 139 within 10s of launch; own addresses absent; synced.

**Files changed**
- `src/GreatEmailApp.Core/Spam/SpamFilterEngine.cs` (Rev 2 → 3)

**Rulebook**
- §2 Surgical Change Rule — bootstrap method + trigger only; classifier untouched.
- Same family as FIX-2026-04-30-002: first-run code on a new PC must not treat "nothing available yet" as a final answer.

**Session:** 2026-11-21
**Commit:** _pending_

---


## FIX-2026-11-21-002 — New PC: accounts showed no folders/mail with no hint that passwords were missing

**Area:** Sidebar / MainViewModel.LoadFoldersAsync · AccountViewModel status · StatusBar
**Status:** ✅ Fixed
**Priority:** P1 (fresh PC looked broken — empty sidebar, grey dots, status bar claiming "Connected")

**Symptom**
- New PC, signed in to sync: all accounts appear in the sidebar but no folders or mail ever load. Account dots stay grey. Status bar says "Connected · All folders are up to date · Last sync 2 min ago".

**Replicate**
1. Fresh PC (no `GreatEmailApp:{accountId}` entries in Credential Manager), sign in → cloud pull restores accounts.json.
2. Sidebar shows the accounts; nothing loads; nothing says why.

**Root cause**
- Expected on a new PC until the password vault is unlocked (rulebook §7C: Settings → Sync → Unlock with passphrase restores all passwords). But three UI bugs hid the state, and the app never pointed at the vault:
  1. `LoadFoldersAsync` set `StatusMessage = "No password stored… Re-add the account."` — then `LoadAccounts` immediately overwrote it with "Loaded N account(s)". And `StatusMessage` wasn't bound anywhere in the main window anyway.
  2. `AccountViewModel.StatusBrush` was computed once in the ctor, so `Model.Status = Error` never repainted the dot (stayed grey).
  3. `StatusBar.xaml` status text + green dot were a hardcoded literal — same anti-pattern as FIX-2026-05-13-001 / FIX-2026-05-07-001.
- There was also no way to enter a password for an existing account short of removing and re-adding it.

**Tried**
- Nothing — diagnosed by reading Credential Manager (`cmdkey /list`: no `GreatEmailApp:` targets) and the load path; fixed on first attempt.

**Fix**
- `AccountViewModel`: `NeedsPassword` flag; `StatusBrush` computed on read; `SetStatus()` raises change notifications.
- `MainViewModel`: missing creds → `NeedsPassword = true` + Error status; "Loaded N" no longer overwrites the warning; `OnPasswordEntered()` reloads folders; `ConnectionBrush` (rose/amber/green/grey) for the status bar; all status writes go through `SetAccountStatus`.
- New `EnterPasswordDialog`: tests the login with `TestConnectionAsync`, then saves to Credential Manager (§7).
- `Sidebar.xaml`: "Password needed · Enter password" button under the account header — placed outside the header Border because the header's `PreviewMouseLeftButtonDown` would swallow the click.
- `StatusBar.xaml`: bound to `StatusMessage` + `ConnectionBrush` instead of the literal.
- Settings → Sync → Unlock with passphrase (vault) still works and already calls `ReloadAccounts`, which clears the flag.

**Files changed**
- `src/GreatEmailApp/ViewModels/AccountViewModel.cs` (Rev 1 → 2)
- `src/GreatEmailApp/ViewModels/MainViewModel.cs` (Rev 13 → 14)
- `src/GreatEmailApp/Views/Dialogs/EnterPasswordDialog.xaml(.cs)` (new, Rev 1)
- `src/GreatEmailApp/Controls/Sidebar.xaml` (Rev 5 → 6), `Sidebar.xaml.cs` (Rev 3 → 4)
- `src/GreatEmailApp/Controls/StatusBar.xaml` (Rev 1 → 2)

**Rulebook**
- §10 — never let visible literals advertise state not bound to a real source (third instance of this anti-pattern).
- §7 — passwords only ever go to Credential Manager.

**Session:** 2026-11-21
**Commit:** _pending_

---

## FIX-2026-11-21-001 — Account monograms showed "JC" for every account

**Area:** Core / account + sender initials
**Status:** ✅ Fixed
**Priority:** P3 (cosmetic, but wrong on every account)

**Symptom**
- Sidebar and Settings → Accounts showed "JC" for jreed@fiksdit.com, james@fiksdit.com and james@fixlogic.co.

**Replicate**
1. Add an account with no display name (display name defaults to the email address).
2. Avatar shows first letter of the local part + first letter of the TLD ("com" / "co").

**Root cause**
- `AddAccountViewModel.MakeInitials` (and an identical copy in `ImapService` for senders) split on `' ', '.', '-', '_'` but not `'@'`: "jreed@fiksdit.com" → ["jreed@fiksdit", "com"] → "JC". The bad value was then persisted in accounts.json and synced to Firestore, so it followed the user to every PC.

**Tried**
- Nothing — fixed on first attempt.

**Fix**
- New `Core/Models/Monogram.cs`: single initials helper; uses only the part before '@' for email addresses ("jreed@…" → "JR", "james@…" → "JA").
- `AddAccountViewModel` and `ImapService` delegate to it.
- `JsonAccountStore` + `FirestoreSyncService` DTO → Account mappings recompute `Initials` (derived, never user-set), so existing bad values self-heal on load and are corrected in the cloud on the next push.

**Files changed**
- `src/GreatEmailApp.Core/Models/Monogram.cs` (new, Rev 1)
- `src/GreatEmailApp.Core/Services/JsonAccountStore.cs` (Rev 2 → 3)
- `src/GreatEmailApp.Core/Sync/FirestoreSyncService.cs` (Rev 2 → 3)
- `src/GreatEmailApp.Core/Services/ImapService.cs` (Rev 4 → 5)
- `src/GreatEmailApp/ViewModels/AddAccountViewModel.cs` (Rev 2 → 3)

**Rulebook**
- §2 Surgical Change Rule — one helper, call sites delegate.

**Session:** 2026-11-21
**Commit:** _pending_

---


## FIX-2026-06-12-001 — Removed built-in spam keyword reappeared on next launch

**Area:** Core / JsonSpamConfigStore + SpamFilterConfig · default-keyword merge
**Status:** ✅ Fixed
**Priority:** P2 (spam keyword editor couldn't permanently remove a default)

**Symptom**
- Settings → Spam → remove a built-in keyword (e.g. "ebitda"), click Done. It's gone from the list. Reopen Settings (or wait for the next poll) and the keyword is back.

**Replicate**
1. Settings → Spam, remove any keyword that ships as a default.
2. Close and reopen the dialog → the keyword has returned.

**Root cause**
- `JsonSpamConfigStore.Load` merges every `SpamFilterConfig.BuiltInKeywords` entry missing from the user's file, so a shipped keyword expansion reaches existing installs. But the merge had no notion of an *intentional* removal — a deleted default was simply "missing," so it got re-added on the very next load. This was a documented Phase-1 limitation (the store's own header earmarked a "RemovedDefaults" list for Phase 2); the spam-keyword editor (T3, this sprint) made it user-reachable, so it had to be closed.

**Tried**
- Nothing — the limitation was already diagnosed and documented in the store's header comment. Implemented the earmarked fix directly.

**Fix**
- `SpamFilterConfig`: added `RemovedDefaults` (List<string>) — built-ins the user explicitly removed.
- `JsonSpamConfigStore.Load`: the default-merge now skips any built-in present in `RemovedDefaults`, so removals stick.
- `SettingsViewModel.SaveSpamConfig`: recomputes `RemovedDefaults` = built-ins not currently in the keyword list on every save, so removing re-adds to the set and re-adding (or "Restore default keywords") clears it.
- Rides the existing `spam_json` Firestore sync, so removals propagate across PCs.

**Files changed**
- `src/GreatEmailApp.Core/Spam/SpamFilterConfig.cs` (Rev 4 → 5)
- `src/GreatEmailApp.Core/Services/JsonSpamConfigStore.cs` (Rev 2 → 3)
- `src/GreatEmailApp/ViewModels/SettingsViewModel.cs` (Rev 6)

**Rulebook**
- §2 Surgical Change Rule — merge logic + one VM method; classifier untouched.
- Completes the Phase-2 follow-up the Phase-1 store header explicitly deferred.

**Session:** 2026-06-12
**Commit:** _pending_

---

## FIX-2026-05-15-001 — New-mail notification toggle silently disabled the entire polling subsystem

**Area:** Notifications / NewMailPoller + TrayNotifier · poll cycle gating
**Status:** ✅ Fixed in v0.12.5
**Priority:** P1 (turning off one checkbox bricked search index, spam filter, rules, and auto-refresh)

**Symptom**
- Settings → Notifications → "New-mail notifications" off → balloons stop (expected) **and** the search indexer, spam filter, rules engine, and mail-list auto-refresh all go dormant (not expected). The only visible symptom was "no balloons"; the actual blast radius was the whole background subsystem.

**Replicate**
1. Turn off new-mail notifications in Settings.
2. Receive new mail. No cache update, no spam routing, no rules, no auto-refresh — nothing fires until the toggle is turned back on.

**Root cause**
- `NewMailPoller.PollOnceAsync` had an early return gated on `App.Settings.EnableNewMailNotifications`. With it false, the poll bailed before raising `MessagesPolled` / `NewMailDetected`, so every downstream subscriber (cache writer, `RulesEngine`, `SpamFilterEngine`, `MainViewModel` auto-refresh) was starved of events. The toggle was wired to the wrong layer — it gated the *poll*, not the *balloon*.
- Side effect: the Phase-1 spam filter built earlier the same session had genuinely never run on incoming mail, because the poller bailed before `SpamFilterEngine`'s `MessagesPolled` handler could fire.

**Tried**
- Nothing — diagnosed on first read of `PollOnceAsync`. The early-return-on-a-notification-flag was the obvious mismatch against the design intent "app open implies polling on."

**Fix**
- `NewMailPoller`: dropped the early return. Polling now always runs while the app is open; subscribers get events on every cycle.
- `TrayNotifier.OnNewMail`: now gates on `App.Settings.EnableNewMailNotifications` before buffering the event, so balloon behavior is unchanged from the user's perspective — the gate just moved to the correct (balloon-only) layer.

**Files changed**
- `src/GreatEmailApp.Core/Notifications/NewMailPoller.cs` — removed poll-gating early return.
- `src/GreatEmailApp/Services/TrayNotifier.cs` — gate balloon on the notification setting.

**Rulebook**
- §2 Surgical Change Rule — only the two responsible files touched; subscriber wiring left alone.
- Decision-log candidate: "A user-facing toggle must gate the narrowest behavior it names. A 'notifications' switch controls notifications, never the data pipeline they observe."

**Session:** 2026-05-15
**Commit:** 70e024f

---

## FIX-2026-05-15-002 — Mail list briefly showed weeks-old ("May 6") mail because the cache sorted display strings, not dates

**Area:** Search / SqliteMessageCache · envelope sort order
**Status:** ✅ Fixed in v0.12.5
**Priority:** P1 (wrong mail on top every time a folder repaints from cache)

**Symptom**
- Reopen the Inbox and, for a flash, weeks-old messages (e.g. dated "May 6") sit above today's mail before the live IMAP fetch corrects it. The cache-paint's top 25 reliably returned Tuesday-dated mail ahead of today's.

**Replicate**
1. Open a folder (paints from cache instantly).
2. Watch the first ~25 rows before the IMAP round-trip completes — order is non-chronological.

**Root cause**
- `SqliteMessageCache` stored `sent_at` as the human-display `FullTime` string (`"Wed, May 13, 2026, 3:22 PM"`), then `SELECT ... ORDER BY sent_at DESC` sorted **lexicographically**. The day-of-week prefix dominated: "Wed" > "Tue" > "Thu" > … — completely independent of the actual date.

**Tried**
- Nothing — diagnosed on first read. The `ORDER BY sent_at DESC` over a column holding `"Wed, May 13…"` strings was self-evidently a lexicographic-vs-chronological bug.

**Fix**
- Added `Message.SentAt` (`DateTimeOffset?`) sourced from MailKit's envelope `Date`.
- `UpsertEnvelopesAsync` now writes ISO 8601 UTC to `sent_at`; `GetEnvelopesAsync` reads it back, repopulates `SentAt` + display strings, and filters its `SELECT` to ISO rows only (`WHERE sent_at LIKE '____-__-__T%'`) so legacy display-format rows are skipped until the next poll rewrites them. **No migration needed — the cache self-heals** on the next poll.

**Files changed**
- `src/GreatEmailApp.Core/Models/Message.cs` — `SentAt` property.
- `src/GreatEmailApp.Core/Search/SqliteMessageCache.cs` — ISO write/read + ISO-only SELECT filter.
- `src/GreatEmailApp.Core/Services/ImapService.cs` — populate `SentAt` from envelope `Date`.

**Rulebook**
- §2 Surgical Change Rule — cache + model only; UI untouched.
- §14 — any ordered list query must sort on a sortable key, never a localized display string.

**Session:** 2026-05-15
**Commit:** 0e81369

---

## FIX-2026-05-15-003 — Mail list didn't reset to the top when switching folders

**Area:** Controls / MailList · ScrollViewer offset on folder change
**Status:** ✅ Fixed in v0.12.5
**Priority:** P2 (navigation annoyance)

**Symptom**
- Click a folder and the list keeps the scroll position from the folder you just left, instead of showing the newest message at the top.

**Replicate**
1. Scroll halfway down folder A.
2. Click folder B → list opens mid-scroll rather than at the top.

**Root cause**
- The `ScrollViewer` wrapping the mail-list `ItemsControl` had no signal to reset its vertical offset when the `Messages` collection was rebuilt via Clear + Add. WPF preserved the prior offset across the rebuild.

**Tried**
- Nothing — diagnosed alongside FIX-2026-05-15-002 (same commit); the missing scroll-reset on collection rebuild was clear from the control wiring.

**Fix**
- `MainViewModel` raises `FolderLoaded` after `SelectFolderAsync` finishes its live IMAP fetch. `MailList.xaml.cs` subscribes via `DataContextChanged` and calls `MessageScroll.ScrollToTop()` on the named `ScrollViewer`.
- Deliberately **not** raised from `RefreshCurrentFolderAsync` (auto-refresh on poll) — preserving the user's scroll position is correct there; a background poll shouldn't yank them away from what they're reading.

**Files changed**
- `src/GreatEmailApp/ViewModels/MainViewModel.cs` — `FolderLoaded` event after folder fetch.
- `src/GreatEmailApp/Controls/MailList.xaml` + `MailList.xaml.cs` — named `ScrollViewer` + `ScrollToTop()` on `FolderLoaded`.

**Rulebook**
- §2 Surgical Change Rule — scoped to folder-switch path; refresh path intentionally excluded.

**Session:** 2026-05-15
**Commit:** 0e81369

---

## FIX-2026-05-15-004 — Reading pane went blank on the selected message after a background auto-refresh

**Area:** ViewModels / MainViewModel + Controls / MessageBodyView · body preservation across refresh
**Status:** ✅ Fixed in v0.12.5
**Priority:** P1 (current message blanks out while reading)

**Symptom**
- While reading a message, the 5-minute poll fires the new auto-refresh and the reading pane for the currently-selected message goes blank.

**Replicate**
1. Open and read a message.
2. Wait for (or trigger) a background poll that refreshes the visible folder.
3. The body pane blanks even though the same message is still selected.

**Root cause**
- `RefreshCurrentFolderAsync` calls `Messages.Clear()` and rebuilds every VM from fresh IMAP envelopes. Envelopes carry headers/preview but **not** `BodyHtml`/`BodyPlain` (those are fetched separately by `SelectMessageAsync`). The reading pane then bound to a brand-new VM with an empty body.

**Tried**
- Nothing — diagnosed on first read. The Clear-and-rebuild in the refresh path dropping the separately-fetched body was the obvious cause; this is the auto-refresh-era sibling of FIX-2026-05-12-001.

**Fix**
- Snapshot `SelectedMessage`'s `BodyHtml` + `BodyPlain` before the clear; copy them onto the new VM during selection-restore; fire `OnBodyLoaded` so `MessageBodyView`'s `PropertyChanged` subscription (FIX-2026-05-12-001) re-renders. The displayed message stays visible across refreshes.

**Files changed**
- `src/GreatEmailApp/ViewModels/MainViewModel.cs` — body snapshot + restore in `RefreshCurrentFolderAsync`.

**Rulebook**
- §2 Surgical Change Rule — refresh path only.
- §10 — when a collection rebuild discards async-fetched state, carry that state forward explicitly; don't assume the new VM has it.

**Session:** 2026-05-15
**Commit:** e3ca017

---

## FIX-2026-05-15-005 — "Auto Send/Receive" said "0 = manual only" but the poller silently clamped 0 to 1 minute

**Area:** Settings / SettingsViewModel + SettingsDialog · sync-interval honesty
**Status:** ✅ Fixed in v0.12.5
**Priority:** P2 (UI promised an off-switch that doesn't exist)

**Symptom**
- Settings → Send/Receive row read "0 = manual only", but entering 0 still polled every minute. `NewMailPoller` force-clamps via `Math.Max(1, SyncIntervalMinutes)`, so 0 was identical to 1.

**Replicate**
1. Set the Auto Send/Receive interval to 0 expecting manual-only.
2. Observe the app keeps polling once a minute.

**Root cause**
- The poller's `Math.Max(1, …)` clamp was correct defensive code, but the Settings description advertised a manual-only mode that was never implemented, and 0 was being persisted (and synced to Firestore) even though it could never take effect.

**Tried**
- Nothing — the mismatch between the description literal and the poller's clamp was a direct read. Owner chose "make the description honest" over "build a real manual-only mode" (the latter left as a separate future change: drop the clamp in `Reschedule` and gate `Start()` on non-zero).

**Fix**
- Description now reads "Check for new mail at this interval. Minimum 1 minute."
- `SettingsViewModel.OnSyncIntervalMinutesChanged` clamps to `>= 1` before writing to `AppSettings`, so the persisted (and synced) value matches what the poller actually runs.

**Files changed**
- `src/GreatEmailApp/ViewModels/SettingsViewModel.cs` — clamp on change.
- `src/GreatEmailApp/Views/Dialogs/SettingsDialog.xaml` — honest description text.

**Rulebook**
- §2 Surgical Change Rule — no behavior change to the poller; only the VM clamp + label.
- Sibling of the spirit behind FIX-2026-05-13-001: never let a visible literal advertise behavior the code doesn't deliver.

**Session:** 2026-05-15
**Commit:** e2d5579

---

## FIX-2026-05-15-006 — Search-box placeholder stayed visible after clicking in, hiding only on first keystroke

**Area:** Controls / TitleBar + MailList · search placeholder focus behavior
**Status:** ✅ Fixed in v0.12.5
**Priority:** P3 (polish; no focus cue against the dark background)

**Symptom**
- Click into the title-bar search box (or the mail-list one) and the placeholder text stays put, only disappearing on the first keystroke. With no clear cursor cue against the dark background, the user can't tell whether the box has focus.

**Replicate**
1. Click into a search box without typing.
2. Placeholder remains; no obvious focus indication.

**Root cause**
- Placeholder visibility was driven by imperative `TextChanged` handlers that only flipped on text content, never on focus.

**Tried**
- Nothing — straightforward: the placeholder needed to react to focus, not just text.

**Fix**
- Both placeholders are now driven by a `MultiDataTrigger` on the parent `TextBlock`'s `Style`: visible only when `Text == ""` **AND** the `TextBox` is not keyboard-focused. Clicking in immediately collapses the placeholder; clicking out with no text restores it. The prior imperative `TextChanged` handlers were removed.

**Files changed**
- `src/GreatEmailApp/Controls/TitleBar.xaml` + `TitleBar.xaml.cs` — `MultiDataTrigger` style; `SearchBox_TextChanged` no longer sets Visibility.
- `src/GreatEmailApp/Controls/MailList.xaml` + `MailList.xaml.cs` — same treatment; `ListSearchBox_TextChanged` handler removed.

**Rulebook**
- §2 Surgical Change Rule — declarative trigger replaces imperative handlers; no other behavior touched.
- §10 — prefer declarative XAML triggers for visual state that depends on focus/content.

**Session:** 2026-05-15
**Commit:** 99bfaab

---

## FIX-2026-05-13-001 — Title-bar "Sync on" chip lied about sync state before sign-in

**Area:** Controls / TitleBar · sync status chip
**Status:** ✅ Fixed
**Priority:** P2 (misleading UI; sync mechanics already worked)

**Symptom**
- Fresh install on a new PC, app launched from desktop shortcut: title-bar chip showed a green dot + "Sync on" before the user had signed into Firebase. Implied that settings/accounts/rules were syncing when nothing was — the user reasonably read it as "sync is not live yet" since sign-in hadn't happened.

**Replicate**
1. Fresh install (no `auth.dat`), launch app.
2. Look at the top-right title bar — green chip says "Sync on" with no qualifier.
3. There is no IMAP account or Firebase session yet.

**Root cause**
- [TitleBar.xaml:159](src/GreatEmailApp/Controls/TitleBar.xaml:159) had a hardcoded `<TextBlock Text="Sync on" />` and a green `<Ellipse Fill="{DynamicResource GreenBrush}" />` — pure visual decoration with no binding to auth or sync state.
- Spiritual sibling of FIX-2026-05-07-001 (avatar pretended a user was signed in when they weren't). That fix repaired the avatar code-behind but missed the visible chip literal in the title-bar XAML.

**Tried**
- Nothing — diagnosed on first read. Grep for `"Sync.{0,5}[Oo]n"` immediately surfaced the hardcoded literal in TitleBar.xaml; cross-checked with SettingsViewModel/SyncCoordinator confirmed sync wiring itself was already correct.

**Fix**
- `MainViewModel`: added `SyncIndicatorVisible` / `SyncIndicatorText` / `SyncIndicatorBrush` / `SyncIndicatorTooltip` observable properties, plus `UpdateSyncIndicator(SyncEvent?)` driven by `App.Auth.SessionChanged` AND `App.SyncCoordinator.StateChanged`. Computes state from `IsSignedIn` + latest `SyncEventKind`.
- `TitleBar`: added 4 matching `SyncChip*` DPs following the existing `AccountInitial`/`AccountEmail` pattern. XAML chip's `Visibility`, `Ellipse.Fill`, `TextBlock.Text`, and `ToolTip` now bind to these DPs. Local `BoolVis` converter declared in `UserControl.Resources`.
- `MainWindow.xaml`: wired MainViewModel's `SyncIndicator*` properties into TitleBar's `SyncChip*` DPs.
- Sync mechanics (SyncCoordinator, FirestoreSyncService, push/pull paths, vault) — untouched. Pure visual binding fix.

**Behavior now:**
| Auth | Coordinator | Chip |
|------|-------------|------|
| Signed-out | — | hidden |
| Signed-in | Idle / Pushed / Applied | green · "Sync on" · "Firebase sync is live. Settings, accounts, contacts, and rules sync across PCs." |
| Signed-in | Pushing / Pulling | accent · "Syncing…" |
| Signed-in | Failed | red · "Sync error" + detail tooltip |

**Files changed**
- `src/GreatEmailApp/ViewModels/MainViewModel.cs` (Rev 6 → 7)
- `src/GreatEmailApp/Controls/TitleBar.xaml.cs` (Rev 4 → 5)
- `src/GreatEmailApp/Controls/TitleBar.xaml` (Rev 1 → 2)
- `src/GreatEmailApp/MainWindow.xaml` (Rev 2 → 3)

**Rulebook**
- §2 Surgical Change Rule — only the 4 binding-related files touched; sync pipeline left alone.
- §10 (Components & UI/UX) — reinforces: never let visible literals advertise state that isn't bound to a real source of truth. Same anti-pattern as FIX-2026-05-07-001 (avatar).

**Session:** 2026-05-13
**Commit:** _pending_

---

## FIX-2026-05-13-002 — "Look for an app in the Microsoft Store" popup after sign-in (mailto:/non-http schemes from email bodies)

**Area:** Controls / MessageBodyView + RichTextEditor · WebView2 link handoff
**Status:** ✅ Fixed
**Priority:** P1 (visible on every email with a mailto: or non-http URI)

**Symptom**
- Windows dialog: "Your PC doesn't have an app that can open this link. Try looking for a compatible app in the Microsoft Store."
- Pops up autonomously after sign-in on a fresh install. Also observed on a second PC running the app yesterday.
- No new-mail balloon visible at the same time. Disabling new-mail notifications in Settings did NOT stop the popup.

**Replicate**
1. Sign in with an account that has HTML emails containing `mailto:` / `tel:` / `webcal:` / tracking-scheme links (any typical marketing email).
2. Select a message in the reading pane, OR let one with `<meta http-equiv="refresh">` auto-fire `OnNavigationStarting`.
3. Popup appears.

**Root cause**
- [MessageBodyView.xaml.cs:225 + :237](src/GreatEmailApp/Controls/MessageBodyView.xaml.cs:225) called `Process.Start(new ProcessStartInfo { FileName = uri, UseShellExecute = true })` for **any** URI WebView2 hands them — no scheme validation. Email-controlled URIs with no registered Windows handler (mailto: without a default mail app, tel:, webcal:, outlook:, tracking schemes) fall back to the shell's "Look for an app in the Microsoft Store" dialog.
- [RichTextEditor.xaml.cs:78](src/GreatEmailApp/Controls/RichTextEditor.xaml.cs:78) had the same hole in the compose-window editor.
- Same bonus security issue: blindly shelling out email-controlled URIs lets a malicious sender invoke any registered scheme handler (`javascript:`, `file:`, custom schemes) via `UseShellExecute=true`.

**Tried**
- **Hypothesis A — modern toast activator (per FIX-2026-05-12-002's wording).** Added `Services/ToastAumid.cs`: `SetCurrentProcessExplicitAppUserModelID("Fiksdit.TheGreatEmailApp")` + writes a Start-menu .lnk with `PKEY_AppUserModel_ID`. Wired from `App.OnStartup`. Build clean, shortcut created at `%APPDATA%\Microsoft\Windows\Start Menu\Programs\The Great Email App.lnk`. Popup still appeared. **Hypothesis rejected** — modern toasts on H.NotifyIcon weren't the trigger.
- **Hypothesis B — Windows toast pipeline regardless of visibility.** Asked user to toggle off new-mail notifications and reproduce. Popup still appeared with notifications fully off. Toast pipeline definitively ruled out.
- Then grep for `Process.Start.*UseShellExecute` surfaced the MessageBodyView call sites; HTML email auto-refresh / mailto: handoff was the obvious match for "autonomous + no visible balloon."

**Fix**
- `MessageBodyView.xaml.cs`: extracted `OpenExternal(string? uri)` helper that whitelists `http`/`https` only. Drops other schemes silently. Both `OnNavigationStarting` and `OnNewWindowRequested` route through it.
- `RichTextEditor.xaml.cs`: inline scheme whitelist in the `NewWindowRequested` handler.
- `Services/ToastAumid.cs` and the `App.OnStartup` call are **kept** as defense-in-depth — they're correct shell hygiene for future toast click-to-open work, even though they weren't the popup's actual fix.

**Behavior change (acceptable, per owner):** `mailto:` links inside email bodies now silently no-op instead of trying to launch a system mail client. Follow-up: route mailto: into an in-app Compose window (separate task).

**Files changed**
- `src/GreatEmailApp/Controls/MessageBodyView.xaml.cs` (Rev 2 → 3) — scheme whitelist
- `src/GreatEmailApp/Controls/RichTextEditor.xaml.cs` (Rev 1 → 2) — scheme whitelist
- `src/GreatEmailApp/Services/ToastAumid.cs` (new) — AUMID + Start-menu .lnk (defense-in-depth)
- `src/GreatEmailApp/App.xaml.cs` (Rev 10 → 11) — calls `ToastAumid.EnsureRegistered()` early in OnStartup

**Rulebook**
- §2 Surgical Change Rule — diagnosis ruled out toast pipeline before code changes landed in MessageBodyView; AUMID work that did land is kept as legitimate hygiene rather than reverted.
- §5 Routes & Security — `UseShellExecute=true` on attacker-controlled URIs is now scoped to http(s) only.
- §11 Error Handling — non-http schemes drop silently; never crash on a link click.
- Spiritual update to FIX-2026-05-12-002's diagnosis: that entry blamed the toast-activation URI handler under crash conditions. With the crash gone, autonomous popups had a different (and previously unknown) cause: the MessageBodyView shell-out path.

**Session:** 2026-05-13
**Commit:** _pending_

---

## FIX-2026-05-12-002 — Tray notifier still crashed on cold start; added ForceCreate on top of FIX-2026-05-10-002

**Area:** Services / TrayNotifier · NewMailPoller balloon delivery
**Status:** ✅ Fixed
**Priority:** P1 (crashes the process on idle)

**Context**
- FIX-2026-05-10-002 wrapped `ShowNotification` in try/catch so the throw stopped killing the process. But the native tray slot was *still* never being registered — H.NotifyIcon's WPF `TaskbarIcon` defers `Shell_NotifyIcon` creation to its Loaded event, and we instantiate the icon from code with no XAML host. So toasts were being silently swallowed AND no tray icon ever appeared in the system tray.

**Symptom (still observed today, even with FIX-2026-05-10-002 deployed locally)**
- `%LOCALAPPDATA%\GreatEmailApp\crash.log` between 10:53 and 11:04 on 2026-05-12 had three identical stacks:
  ```
  System.InvalidOperationException: TrayIcon is not created.
    at H.NotifyIcon.Core.TrayIcon.EnsureCreated()
    at H.NotifyIcon.TaskbarIcon.ShowNotification(...)
    at GreatEmailApp.Services.TrayNotifier.ShowBalloon(...)
  ```
- Windows then surfaced "Look for an app in the Microsoft Store to open this link" — the .NET crash recovery path falling through to the toast-activation URI handler that has no registered protocol for this raw (non-MSIX, no AppUserModelID) EXE.

**Tried**
- Nothing — diagnosed straight from the crash.log stacks plus the H.NotifyIcon source pointer in the trace.

**Fix**
- `TrayNotifier` ctor now calls `_icon.ForceCreate()` immediately after configuring icon + menu, wrapped in try/catch with a `crash.log` append. This proactively registers the native tray slot at startup instead of waiting on a never-firing Loaded event, so subsequent `ShowNotification` calls find a created icon. The catch path keeps the constructor unkillable.
- `ShowBalloon` already had the `try / catch (InvalidOperationException) / catch (Exception)` from FIX-2026-05-10-002 — kept in place as belt-and-suspenders.

**Files changed**
- `src/GreatEmailApp/Services/TrayNotifier.cs` (Rev 3 → 4)

**Rulebook**
- §11 (Error Handling) — toast/balloon delivery is non-critical I/O; never propagate to a process-killing exception.
- §2 Surgical Change Rule — only TrayNotifier touched.

**Session:** 2026-05-12

---

## FIX-2026-05-12-001 — Reading pane stayed blank when switching emails in list view

**Area:** Controls / MessageBodyView · Reading pane body render
**Status:** ✅ Fixed
**Priority:** P1 (core reading flow)

**Symptom**
- Click a message in the middle pane → headers, avatar, subject, To: line all update — but the body area in the WebView2 stays blank (or shows the previously rendered body for a flash, then goes blank).
- Workaround discovered by users: click away to a different message and back; sometimes works on the third try. Inconsistent.

**Replicate**
1. Open Inbox, click message A. Body renders fine (first selection after launch).
2. Click message B. Headers swap to B. Body pane is blank.
3. Click message A again. Sometimes A's body comes back, sometimes blank.

**Root cause**
- `SelectMessageAsync` sets `SelectedMessage = B` first → WPF re-binds `ReadingPane`'s inner DataContext → `MessageBodyView.Message` DP changes → `OnMessageChanged` fires → `Render()` runs **synchronously** reading `B.BodyHtml`/`B.BodyPlain`, both still empty at this point → WebView2 renders the empty wrapper doc.
- THEN `_imap.FetchBodyAsync(B, …)` returns ~200 ms later → assigns the strings on the model → calls `B.OnBodyLoaded()` which fires `PropertyChanged` for `BodyHtml`, `BodyPlain`, `BodyDisplay`.
- **Nothing in `MessageBodyView` listened for those events.** Its only render trigger was the `Message` DP changed callback — and the DP value (the `MessageViewModel` reference) was unchanged.
- `MainViewModel` tried to nudge things with `OnPropertyChanged(nameof(SelectedMessage))`, but the binding's new value reference-equals the old, so WPF's DP system short-circuits and `OnMessageChanged` isn't re-invoked.

**Tried**
- Nothing — diagnosed on first read of `MessageBodyView.Render()` + `OnMessageChanged` + the body-fetch tail of `SelectMessageAsync`. The control's `Refresh()` method existed but was never called, which was the tell.

**Fix**
- `MessageBodyView` now tracks `_subscribedMessage` and subscribes to `INotifyPropertyChanged.PropertyChanged` on the bound `MessageViewModel`. When `BodyHtml`/`BodyPlain`/`BodyDisplay` fires, it re-calls `Render()`. Switching messages detaches the previous subscription first to prevent leaks.

**Files changed**
- `src/GreatEmailApp/Controls/MessageBodyView.xaml.cs` (Rev 1 → 2)

**Rulebook**
- §2 Surgical Change Rule — touched only the file with the missing wiring.
- §10 (Components & UI/UX) → reinforces: DP-only re-render is a trap when the model behind the DP mutates async. Listen to `PropertyChanged` on the model when its inner state can change after the DP is set.

**Session:** 2026-05-12

---


## FIX-2026-05-10-002 — App crashed on first launch, ran fine on second (concurrent settings write + tray race)

**Area:** SyncCoordinator + JsonSettingsStore + TrayNotifier
**Status:** ✅ Fixed in v0.11.11
**Priority:** P1 — visible to every user on every cold start

**Symptom**
- Launch app → process exits silently within seconds.
- Reopen → fine.
- Manually close → next cold launch crashes again. Loop.

**Root cause — two independent crashes both fired during startup**

1. **Concurrent settings.json writes.** `App.RestoreAndStartSyncAsync()` ran two paths back-to-back:
   - `await Auth.TryRestoreAsync()` raised `SessionChanged` → kicked off `PullOrSeedAsync` on a worker thread.
   - Then `await SyncCoordinator.StartAsync()` called `PullOrSeedAsync` *again* because auth was now signed in.
   - Both `ApplyRemote` paths reached `JsonSettingsStore.Save()` simultaneously. They share the same `settings.json.tmp` filename, so `File.WriteAllText(tmp, …)` on the second one threw `IOException: file in use`. Unhandled → process death.

2. **Tray balloon before tray was realized.** `TrayNotifier.ShowBalloon` ran from a `MailPoller.NewMailDetected` dispatch. On first launch the H.NotifyIcon `Shell_NotifyIcon` registration hadn't completed yet, so `ShowNotification` threw `InvalidOperationException: TrayIcon is not created.` Unhandled → process death.

The reason "second launch is fine": the cloud snapshot was already up-to-date by then (the partial first-run had pushed local), and the poller had no fresh mail to balloon.

**Tried**
- Nothing — diagnosed straight from `crash.log` (which had captured both stack traces from prior runs).

**Fix**
- `SyncCoordinator`: added `SemaphoreSlim _gate (1,1)`. Public `PushAsync` / `PullAsync` / `PullOrSeedAsync` acquire it and delegate to private `*CoreAsync` variants. Internal callers (e.g. `PullCore` falling back to push when local prefers) call the Core variants directly to avoid re-entrant deadlock. Disposed in Dispose.
- `JsonSettingsStore.Save`: wrapped the `.tmp + Move` in `lock (_saveLock)` for belt-and-suspenders against any other concurrent caller.
- `TrayNotifier.ShowBalloon`: wrapped `ShowNotification` in `try/catch (InvalidOperationException) / catch (Exception)`. Notifications are best-effort.

**Files changed**
- `src/GreatEmailApp.Core/Sync/SyncCoordinator.cs` — Rev 5: gate + Core split.
- `src/GreatEmailApp.Core/Services/JsonSettingsStore.cs` — Rev 3: save lock.
- `src/GreatEmailApp/Services/TrayNotifier.cs` — Rev 3: swallow tray-not-created.
- `src/GreatEmailApp/GreatEmailApp.csproj` — version 0.11.11.

**Verified**
- Cold start: alive, no crash.log written.
- Kill + relaunch cycle: both runs alive, no crash.log written.

**Rulebook**
- No new rule needed; existing §16 applies. Decision-log candidate: "Any code path that runs both at app-startup and from an event handler that startup itself raises must be safe to run concurrently — prefer a coordinator-level gate over per-callsite ordering."

---

## FIX-2026-05-10-001 — Sync silently flipped pulls into pushes; new PCs got zero accounts

**Area:** Sync / SyncCoordinator + SyncMetadata
**Status:** ✅ Fixed in v0.11.5
**Priority:** P0 — silent data divergence between PCs

**Symptom**
- New PC signed into Firebase, expecting 3 accounts to pull down from cloud. Got nothing — only the 1 account already present locally stayed. Settings → Sync → "Sync now" reported success.
- On a PC that had successfully pulled once, every subsequent pull was actually pushing the local roster to the cloud, overwriting whatever the other PC had pushed.

**Root cause**
Two compounding bugs in the FIX-2026-04-30-002 guard:

1. `SyncMetadata.HasUnpushedLocalChanges()` used `threshold.AddSeconds(-2)` for filesystem-mtime slop. That widens "has unpushed changes" instead of narrowing it — any file mtime within 2s of LastSyncedAt counted as unpushed. Wrong direction.
2. `SyncCoordinator.ApplyRemote()` set `_meta.LastSyncedAt = remote.UpdatedAt` (the snapshot's earlier server-side timestamp). But `ApplyRemote` had just written `accounts.json`, so its mtime = "now", which is later than `remote.UpdatedAt`. Combined with bug #1, this made every just-pulled device permanently look "unpushed" → next pull triggered `ShouldPreferLocalOver` → push the stale local data instead of applying remote.

User's symptom: PC A had 3 accounts, PC B had 1. Both kept pushing their own list and ignoring remote. Whichever PC last synced won the cloud doc — so PC B's recent activity had clobbered PC A's roster.

**Tried**
- Nothing — diagnosed directly from `sync-meta.json` mtime vs `accounts.json` mtime on the user's machine: file mtime `15:27:50.000`, LastSyncedAt `15:27:50.523`. With `-2s` slop, file looks newer; bug confirmed by inspection.

**Fix**
- `SyncMetadata.HasUnpushedLocalChanges`: slop direction inverted — `threshold.AddSeconds(+2)`. Only files clearly newer than baseline count as unpushed.
- `SyncCoordinator.ApplyRemote`: `_meta.LastSyncedAt = DateTimeOffset.UtcNow` after the local Save calls complete, so the new baseline is strictly later than any file we just wrote.
- FIX-2026-04-30-002's empty-cloud guard (clause 2 of `ShouldPreferLocalOver`) is unchanged — that protection was independent of the timestamp comparison and still blocks empty cloud from clobbering local.

**Files changed**
- `src/GreatEmailApp.Core/Sync/SyncMetadata.cs` — Rev 2: slop direction.
- `src/GreatEmailApp.Core/Sync/SyncCoordinator.cs` — Rev 3: ApplyRemote baseline timestamp.
- `src/GreatEmailApp/GreatEmailApp.csproj` — version 0.11.5.

**Recovery for the user**
1. Install 0.11.5 on **both** PCs (built-in updater, or copy install dir).
2. On the PC that has the 3 accounts: Settings → Sync → Sync now (push).
3. On the other PC: Settings → Sync → Sync now (pull). Roster appears.

**Rulebook**
- No new rule needed; existing §16 applies. Decision-log entry in roadmap may want a one-liner that timestamp slop in sync guards must always be in the conservative direction (treat near-equal as in-sync).

---

## FIX-2026-05-07-001 — Avatar pretended a user was signed in when they weren't

**Area:** TitleBar / Avatar button
**Status:** ✅ Fixed
**Priority:** P2

**Symptom**
- Top-right avatar always showed "JR" with a tooltip of `coolman0804@outlook.com`, regardless of whether anyone was actually signed into Firebase. Clicking it showed a `MessageBox` reading "Signed in: coolman0804@outlook.com / Sync: on".
- Misleading: implied an active account where none existed. Worse, the only path to actually sign in was buried in Settings → Sync — the avatar gave no hint of that.

**Replicate**
1. Fresh install (no `auth.dat`), launch app.
2. Hover top-right avatar → tooltip "coolman0804@outlook.com".
3. Click avatar → popup claims you're signed in.

**Root cause**
- `MainViewModel.AccountInitial` and `AccountEmail` were hardcoded literals (`"JR"`, `"coolman0804@outlook.com"`) — leftover sample-data from Phase 1 that never got reconnected to the real `IAuthService` when Phase 4 auth landed.
- `TitleBar.AvatarButton_Click` showed a placeholder `MessageBox` with no signed-out branch.

**Tried**
- Nothing — fixed on first attempt. Root cause was visible in a 3-line grep for `coolman` in the source tree.

**Fix**
- `MainViewModel.AccountInitial` / `AccountEmail` / `IsSignedIn` now derive from `App.Auth.Current`. Signed-out → initial `?`, tooltip `"Sign in"`.
- Subscribed to `App.Auth.SessionChanged` in the VM ctor so the avatar repaints on sign-in / sign-out / silent restore (dispatched onto the UI thread).
- `TitleBar.AvatarButton_Click` now branches on `App.Auth?.IsSignedIn`: when signed-out, opens `SettingsDialog` directly on the **Sync** tab via `OpenOnTab("Sync")`. When signed-in, keeps the lightweight info popup (Phase-4 placeholder line removed).

**Files changed**
- `src/GreatEmailApp/ViewModels/MainViewModel.cs` (Rev 2 → 3)
- `src/GreatEmailApp/Controls/TitleBar.xaml.cs` (Rev 2 → 3)

**Rulebook**
- §2 Surgical Change Rule — touched only the two responsible files, no XAML or auth-service changes.
- Spiritual sibling of FIX-2026-04-30-002: same "fake data masquerading as real" anti-pattern, different surface (avatar instead of message list).

**Session:** 2026-05-07
**Commit:** _pending_

---

## FIX-2026-04-30-002 — Auto-sync wiped local accounts on first 0.4.0 launch

**Symptom**
- After installing 0.4.0 (which added auto-pull on startup), user's `accounts.json` was rewritten to `[]` and the sidebar fell back to sample data.
- The cloud document had been seeded with an empty accounts array on the very first sign-in (before any real account was added). Every subsequent startup pulled that stale empty snapshot and clobbered local state.

**Tried**
- Nothing — caught on first user report. Root cause was clear from `accounts.json`'s mtime matching the SyncCoordinator's pull-on-start window.

**Fix**
- New `SyncMetadata` (`%LOCALAPPDATA%\GreatEmailApp\sync-meta.json`) tracks `LastSyncedAt`. Local-only, never pushed.
- `SyncCoordinator.ShouldPreferLocalOver(remote)` returns true when:
  1. Local files were modified after `LastSyncedAt` (unpushed edits exist), OR
  2. Local has accounts but remote does not, AND remote isn't strictly newer than `LastSyncedAt`.
- When that fires, the coordinator **pushes local instead of applying remote**.
- Removed sample-data fallback from `MainViewModel.LoadAccounts` so an empty roster is just an empty UI ("No accounts yet — click Add account") instead of fake data masquerading as real.

**Files changed**
- `src/GreatEmailApp.Core/Storage/AppPaths.cs` — `SyncMetaJson` path.
- `src/GreatEmailApp.Core/Sync/SyncMetadata.cs` — new.
- `src/GreatEmailApp.Core/Sync/SyncCoordinator.cs` — wires metadata into Push + Pull + PullOrSeed; `ShouldPreferLocalOver` decision; updates `_meta.LastSyncedAt` on every push and apply-pull.
- `src/GreatEmailApp/ViewModels/MainViewModel.cs` — empty-state replaces SampleData fallback; dead `LoadSampleMessages` removed.
- `src/GreatEmailApp.Core/Sample/SampleData.cs` — deleted.

**Rulebook**
- Existing decision log entry "Last-write-wins for settings sync conflicts" still holds. The fix layers a "did we already see that write?" check on top: a remote with a timestamp older than our last sync is treated as stale relative to subsequent local edits.

---

## FIX-2026-04-30-001 — White-window on launch (WPF hardware rendering broken)

**Symptom**
- App launches and reaches `MainWindow.ContentRendered` cleanly, no exceptions.
- Window chrome + entire client area paint as **pure white**. Mouse cursor changes to a pointer over interactive zones (so the visual tree is hit-testing correctly), but nothing draws.
- Reproduced on a fresh Win11 box. Same code on the user's other PC renders correctly.

**Tried**
- Cleaned `bin/`, `obj/`, full rebuild — no change.
- Verified `Themes/Dark.xaml` loads (30 keys present in `Application.Current.Resources.MergedDictionaries[1]`).
- `TryFindResource("AppBackgroundBrush")` returns `#FF1F1F1F` — brushes are resolvable, the data is correct.
- Skipped `ThemeManager.Apply()` entirely, relying on App.xaml's parse-time merged dictionaries. Still pure white. So Apply() wasn't to blame.
- `RenderCapability.Tier >> 16` reports **Tier 2** (full hardware acceleration available) but the GPU pipeline produces a blank surface.

**Fix**
Force `RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly` at the very top of `App.OnStartup`, before `base.OnStartup(e)`. WPF's software rasterizer is plenty fast for an email-client UI and removes a whole class of GPU-driver-dependent rendering bugs.

**Files changed**
- `src/GreatEmailApp/App.xaml.cs` (Rev 7) — set `ProcessRenderMode` first thing in `OnStartup`.

**Rulebook**
- New §17 (Rendering) added: "WPF process render mode is SoftwareOnly. Don't change without testing on every supported PC."
- Preflight Step 00 updated with the symptom signature so it's recognized on the next fresh-PC bringup instead of being re-debugged.
