// panes.jsx — Sidebar (accounts/folders), Mail list, Reading pane

const { useState: useStateP, useRef: useRefP, useEffect: useEffectP } = React;

function Sidebar({ accounts, selectedFolder, onSelectFolder, onAddAccount }) {
  const [expanded, setExpanded] = useStateP({ primary: true, work: true, side: false, alumni: false, legacy: false });
  const toggle = (id) => setExpanded(e => ({ ...e, [id]: !e[id] }));

  return (
    <>
      <div className="sidebar-compose">
        <button className="sidebar-compose-btn">
          <Icon name="newMail" size={16} />
          New Email
        </button>
      </div>
      <div className="sidebar-tree">
        {accounts.map(acct => (
          <div key={acct.id} className="account">
            <div className={`account-row`} onClick={() => toggle(acct.id)}>
              <span className={`chevron ${expanded[acct.id] ? "expanded" : ""}`}>
                <Icon name="chevron" size={12} />
              </span>
              <span className="account-mark" style={{ background: acct.color }}>{acct.initials}</span>
              <span className="account-name">{acct.short}</span>
              <span className={`account-status ${acct.status}`} title={acct.status} />
            </div>
            {expanded[acct.id] && (
              <div className="folder-list">
                {acct.folders.map(f => (
                  <div
                    key={f.id}
                    className={`folder-row ${f.nested ? "nested" : ""} ${selectedFolder === f.id ? "selected" : ""} ${f.count > 0 && f.special ? "unread" : ""}`}
                    onClick={() => onSelectFolder(f.id)}
                  >
                    <span className="folder-icon"><Icon name={f.icon} size={14} /></span>
                    <span className="folder-name">{f.name}</span>
                    {f.count > 0 && <span className="folder-count">{f.count}</span>}
                  </div>
                ))}
              </div>
            )}
          </div>
        ))}
      </div>
      <div className="sidebar-add">
        <button onClick={onAddAccount}>
          <Icon name="plus" size={12} />
          Add Account
        </button>
      </div>
    </>
  );
}

function MailList({ emails, selectedId, onSelect, filter, setFilter }) {
  const groups = {};
  emails.forEach(e => {
    if (!groups[e.group]) groups[e.group] = [];
    groups[e.group].push(e);
  });

  const filterCounts = {
    All: emails.length,
    Unread: emails.filter(e => e.unread).length,
    Flagged: emails.filter(e => e.flagged).length,
    Mentions: 0
  };

  return (
    <>
      <div className="maillist-header">
        <h2>Inbox <span className="folder-count-large">· {emails.length} items, {filterCounts.Unread} unread</span></h2>
        <div className="maillist-search-wrap">
          <Icon name="search" size={14} />
          <input className="maillist-search" placeholder="Search this folder…" />
        </div>
        <div className="filter-pills">
          {["All", "Unread", "Flagged", "Mentions"].map(f => (
            <button
              key={f}
              className={`filter-pill ${filter === f ? "active" : ""}`}
              onClick={() => setFilter(f)}
            >
              {f}
              {filterCounts[f] > 0 && <span className="pill-count">{filterCounts[f]}</span>}
            </button>
          ))}
        </div>
      </div>
      <div className="maillist-scroll">
        {Object.entries(groups).map(([group, items]) => (
          <React.Fragment key={group}>
            <div className="mail-group-header">{group}</div>
            {items.map(e => (
              <div
                key={e.id}
                className={`mail-row ${e.unread ? "unread" : ""} ${selectedId === e.id ? "selected" : ""}`}
                onClick={() => onSelect(e.id)}
              >
                <div className="mail-avatar" style={{ background: e.color }}>{e.avatar}</div>
                <div className="mail-body">
                  <div className="mail-line1">
                    <span className="mail-sender">{e.sender}</span>
                    <span className="mail-time">{e.time}</span>
                  </div>
                  <div className="mail-subject">{e.subject}</div>
                  <div className="mail-preview">{e.preview}</div>
                </div>
                <div className="mail-meta">
                  <div className="mail-flags">
                    {e.attachments && e.attachments.length > 0 && <span className="flag-attach"><Icon name="paperclip" size={13} /></span>}
                    {e.important && <span className="flag-important"><Icon name="alert" size={13} /></span>}
                    {e.flagged && <span className="flag-on"><Icon name="flagFilled" size={13} /></span>}
                  </div>
                  {e.color && <span className="mail-category-dot" style={{ background: e.color }} />}
                </div>
              </div>
            ))}
          </React.Fragment>
        ))}
      </div>
    </>
  );
}

function ReadingPane({ email }) {
  if (!email) {
    return (
      <div style={{ display: "grid", placeItems: "center", flex: 1, color: "var(--text-tertiary)", fontSize: 14 }}>
        Select a message to read.
      </div>
    );
  }
  return (
    <>
      <div className="reading-actionbar">
        <button className="reading-action"><span className="ra-icn"><Icon name="reply" size={15} /></span> Reply</button>
        <button className="reading-action"><span className="ra-icn"><Icon name="replyAll" size={15} /></span> Reply All</button>
        <button className="reading-action"><span className="ra-icn"><Icon name="forward" size={15} /></span> Forward</button>
        <div style={{ width: 1, height: 20, background: "var(--divider)", margin: "0 4px" }} />
        <button className="reading-action rose"><span className="ra-icn"><Icon name="trash" size={15} /></span> Delete</button>
        <button className="reading-action green"><span className="ra-icn"><Icon name="archive" size={15} /></span> Archive</button>
        <button className="reading-action rose"><span className="ra-icn"><Icon name="flagFilled" size={15} /></span> Flag</button>
        <div style={{ flex: 1 }} />
        <button className="reading-action"><span className="ra-icn"><Icon name="more" size={15} /></span></button>
      </div>
      <div className="reading-scroll">
        <div className="reading-header">
          <h1 className="reading-subject">{email.subject}</h1>
          <div className="reading-meta">
            <div className="reading-avatar" style={{ background: email.color }}>{email.avatar}</div>
            <div className="reading-meta-text">
              <div>
                <span className="reading-from">{email.sender}</span>
                <span className="reading-from-email">&lt;{email.senderEmail}&gt;</span>
              </div>
              <div className="reading-recipients">
                To: {email.to || "you"} <span className="expand">· Show Cc/Bcc</span>
              </div>
            </div>
            <div className="reading-time">{email.fullTime || email.time}</div>
          </div>
          {email.attachments && email.attachments.length > 0 && (
            <div className="reading-attachments">
              {email.attachments.map((a, i) => (
                <div key={i} className="attach-chip">
                  <div className="attach-chip-icon" style={{ background: a.color || "var(--accent)" }}>{a.ext}</div>
                  <div>
                    <div className="attach-chip-name">{a.name}</div>
                    <div className="attach-chip-size">{a.size}</div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
        <div className="reading-body">
          <NewsletterContent />
        </div>
      </div>
    </>
  );
}

// Newsletter HTML email body — designed mock content.
function NewsletterContent() {
  return (
    <div style={{ fontFamily: "Georgia, 'Iowan Old Style', serif", color: "var(--text-primary)" }}>
      <div style={{
        display: "flex", alignItems: "center", justifyContent: "space-between",
        paddingBottom: 14, borderBottom: "2px solid var(--text-primary)", marginBottom: 22
      }}>
        <div>
          <div style={{ fontFamily: "var(--font-display)", fontSize: 11, letterSpacing: ".15em", textTransform: "uppercase", color: "var(--text-tertiary)" }}>
            Field Notes Weekly · Issue 142
          </div>
          <div style={{ fontFamily: "var(--font-display)", fontSize: 12, color: "var(--text-secondary)", marginTop: 2 }}>
            Tuesday, April 28, 2026
          </div>
        </div>
        <div style={{ fontFamily: "Georgia, serif", fontSize: 28, fontWeight: 700, fontStyle: "italic", letterSpacing: "-0.02em" }}>
          F<span style={{ color: "var(--green)" }}>N</span>
        </div>
      </div>

      <div style={{
        height: 220, borderRadius: 8, marginBottom: 22, overflow: "hidden", position: "relative",
        background: "linear-gradient(135deg, #14a37f 0%, #3A6FF8 60%, #8a5cf5 100%)"
      }}>
        <div style={{ position: "absolute", inset: 0, opacity: .25,
          backgroundImage: "repeating-linear-gradient(45deg, rgba(255,255,255,.15) 0 2px, transparent 2px 16px)" }} />
        <div style={{ position: "absolute", left: 24, bottom: 20, color: "white" }}>
          <div style={{ fontFamily: "var(--font-mono)", fontSize: 10, letterSpacing: ".1em", opacity: .85 }}>COVER · ISSUE 142</div>
          <div style={{ fontFamily: "Georgia, serif", fontSize: 26, fontStyle: "italic", marginTop: 6, maxWidth: 320, lineHeight: 1.15 }}>
            On craft, calm software, and the slow web.
          </div>
        </div>
      </div>

      <p style={{ fontSize: 17, lineHeight: 1.55, marginTop: 0, marginBottom: 18, fontStyle: "italic", color: "var(--text-secondary)" }}>
        Welcome back. This week we're slowing down — three small tools that get out of your way, a long read on workshop discipline, and a question we keep hearing: what does it mean to ship calm software?
      </p>

      <h2 style={{ fontFamily: "var(--font-display)", fontSize: 19, fontWeight: 600, color: "var(--accent)", margin: "28px 0 8px" }}>
        The case for slower interfaces
      </h2>
      <p style={{ marginTop: 0, fontSize: 15, lineHeight: 1.65 }}>
        The software industry has spent two decades optimizing for engagement. Streaks. Dots. Notifications that pulse. There's a quieter movement now of small studios building <em>opposite</em>: tools that surface less, interrupt rarely, and respect the fact that you have a life outside your screen. We talked to four makers about what calm means in practice — and why it's harder to ship than it sounds.
      </p>
      <p style={{ fontSize: 15, lineHeight: 1.65 }}>
        <a href="#" style={{ color: "var(--accent)", textDecoration: "none", fontWeight: 600, borderBottom: "1px solid var(--accent)" }}>Read the full piece →</a>
      </p>

      <h2 style={{ fontFamily: "var(--font-display)", fontSize: 19, fontWeight: 600, color: "var(--green)", margin: "28px 0 8px" }}>
        Three small tools, one weekend
      </h2>
      <ol style={{ paddingLeft: 22, fontSize: 15, lineHeight: 1.65 }}>
        <li style={{ marginBottom: 6 }}><b>Quill.app</b> — a markdown notebook that boots in 80ms and forgets you exist.</li>
        <li style={{ marginBottom: 6 }}><b>Tide</b> — a tiny menu-bar timer with no streaks, no badges, no stats.</li>
        <li style={{ marginBottom: 6 }}><b>Postroom</b> — IMAP email that shows up only when you open it.</li>
      </ol>

      <div style={{
        background: "var(--bg-pane-2)", border: "1px solid var(--border)", borderLeft: "3px solid var(--green)",
        padding: "14px 18px", borderRadius: 6, margin: "22px 0", fontSize: 14, lineHeight: 1.55, fontStyle: "italic", color: "var(--text-secondary)"
      }}>
        "The hardest part isn't building the feature. It's the discipline to leave it out."
        <div style={{ fontStyle: "normal", fontSize: 12, color: "var(--text-tertiary)", marginTop: 8, fontFamily: "var(--font-display)" }}>— Aiko Tanaka, in conversation</div>
      </div>

      <h2 style={{ fontFamily: "var(--font-display)", fontSize: 19, fontWeight: 600, color: "var(--violet)", margin: "28px 0 8px" }}>
        Long read: workshop discipline
      </h2>
      <p style={{ fontSize: 15, lineHeight: 1.65 }}>
        Aiko Tanaka spent six months in a Kyoto carpentry workshop and came back with a theory of how independent software studios should run. It's about constraints, weekly rhythm, and the small dignity of finishing things. <a href="#" style={{ color: "var(--accent)", fontWeight: 600 }}>Read on the web</a>.
      </p>

      <div style={{
        marginTop: 36, paddingTop: 18, borderTop: "1px solid var(--divider)",
        fontSize: 12, color: "var(--text-tertiary)", fontFamily: "var(--font-display)", textAlign: "center"
      }}>
        Field Notes Weekly · Sent to me@somemail.io · <a href="#" style={{ color: "var(--text-tertiary)" }}>unsubscribe</a> · <a href="#" style={{ color: "var(--text-tertiary)" }}>view in browser</a>
      </div>
    </div>
  );
}

window.Sidebar = Sidebar;
window.MailList = MailList;
window.ReadingPane = ReadingPane;
