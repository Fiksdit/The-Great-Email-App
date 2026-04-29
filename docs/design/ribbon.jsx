// ribbon.jsx — Title bar + Ribbon (tabs, simplified row, classic groups)

const { useState, useRef, useEffect } = React;

function TitleBar({ onAvatarClick, onShowSearch, theme }) {
  return (
    <div className="titlebar">
      <div className="titlebar-brand">
        <div className="titlebar-brand-mark">T</div>
        <span>The Great Email App</span>
        <span style={{ color: "var(--text-tertiary)", fontWeight: 400, marginLeft: 4 }}>— Inbox</span>
      </div>
      <div className="titlebar-search" onClick={onShowSearch}>
        <Icon name="search" size={14} />
        <input placeholder="Search mail and people…" readOnly />
        <span className="kbd">Ctrl K</span>
      </div>
      <div className="titlebar-spacer" />
      <div className="titlebar-actions">
        <button className="titlebar-icon-btn" title="Notifications">
          <Icon name="bell" size={16} />
        </button>
        <button className="titlebar-avatar" onClick={onAvatarClick} title="Account">M</button>
        <div className="win-controls">
          <button className="win-control"><Icon name="minimize" size={10} /></button>
          <button className="win-control"><Icon name="maximize" size={10} /></button>
          <button className="win-control close"><Icon name="close" size={10} /></button>
        </div>
      </div>
    </div>
  );
}

function AvatarPopover({ onClose, onSignOut, syncOn, setSyncOn }) {
  return (
    <div className="popover" style={{ top: 40, right: 16, width: 280 }}>
      <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 12 }}>
        <div style={{
          width: 44, height: 44, borderRadius: "50%",
          background: "linear-gradient(135deg, var(--accent), var(--green))",
          display: "grid", placeItems: "center", color: "white", fontWeight: 600
        }}>M</div>
        <div style={{ minWidth: 0 }}>
          <div style={{ fontWeight: 600, fontSize: 13 }}>Morgan Dixon</div>
          <div style={{ fontSize: 11.5, color: "var(--text-tertiary)" }}>m.dixon@gmail.com</div>
        </div>
      </div>
      <div style={{ borderTop: "1px solid var(--divider)", paddingTop: 12, display: "flex", flexDirection: "column", gap: 4 }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", padding: "6px 0" }}>
          <div>
            <div style={{ fontSize: 12.5, fontWeight: 500 }}>Settings sync</div>
            <div style={{ fontSize: 11, color: "var(--text-tertiary)" }}>Backup via Google account</div>
          </div>
          <label className="switch">
            <input type="checkbox" checked={syncOn} onChange={(e) => setSyncOn(e.target.checked)} />
            <span className="switch-track" />
          </label>
        </div>
        <button className="rb-btn" style={{ width: "100%", justifyContent: "flex-start", height: 32 }} onClick={onSignOut}>
          <Icon name="signOut" size={14} />
          Sign out of Google
        </button>
      </div>
    </div>
  );
}

// Simplified ribbon contents per tab.
const SIMPLIFIED = {
  Home: [
    { kind: "btn", icon: "newMail", label: "New Email", primary: true },
    { kind: "btn", icon: "drafts", label: "New Items", color: "blue" },
    { kind: "div" },
    { kind: "btn", icon: "trash", label: "Delete", color: "rose" },
    { kind: "btn", icon: "archive", label: "Archive", color: "green" },
    { kind: "div" },
    { kind: "btn", icon: "reply", label: "Reply", color: "blue" },
    { kind: "btn", icon: "replyAll", label: "Reply All", color: "blue" },
    { kind: "btn", icon: "forward", label: "Forward", color: "blue" },
    { kind: "div" },
    { kind: "btn", icon: "folder", label: "Move" },
    { kind: "btn", icon: "rules", label: "Rules", color: "violet" },
    { kind: "btn", icon: "alert", label: "Mark Unread", color: "amber" },
    { kind: "btn", icon: "category", label: "Categorize", color: "violet" },
    { kind: "btn", icon: "flag", label: "Follow Up", color: "rose" },
    { kind: "div" },
    { kind: "btn", icon: "people", label: "Search People" }
  ],
  "Send/Receive": [
    { kind: "btn", icon: "sync", label: "Send/Receive All", primary: true },
    { kind: "btn", icon: "reload", label: "Update Folder", color: "blue" },
    { kind: "btn", icon: "send", label: "Send All", color: "green" },
    { kind: "div" },
    { kind: "btn", icon: "offline", label: "Work Offline", color: "amber" },
    { kind: "btn", icon: "cancel", label: "Cancel", color: "rose" },
    { kind: "btn", icon: "progress", label: "Show Progress", color: "violet" }
  ],
  View: [
    { kind: "btn", icon: "view", label: "Reading Pane", color: "blue" },
    { kind: "btn", icon: "layout", label: "Folder Pane", color: "blue" },
    { kind: "btn", icon: "reload", label: "Reset View" },
    { kind: "div" },
    { kind: "btn", icon: "arrange", label: "Sort By", color: "violet" },
    { kind: "btn", icon: "filter", label: "Arrange By", color: "violet" },
    { kind: "btn", icon: "density", label: "Layout Density", color: "green" }
  ],
  Help: [
    { kind: "btn", icon: "info", label: "About", color: "blue" },
    { kind: "btn", icon: "settings", label: "Settings", primary: true },
    { kind: "btn", icon: "people", label: "Account Settings", color: "violet" },
    { kind: "btn", icon: "download", label: "Check for Updates", color: "green" }
  ]
};

// Classic ribbon: groups of large + small buttons.
const CLASSIC = {
  Home: [
    { label: "New", items: [
      { icon: "newMail", label: "New\nEmail", large: true, primary: true },
      { icon: "drafts", label: "New\nItems", large: true, color: "blue" }
    ]},
    { label: "Delete", items: [
      { icon: "trash", label: "Delete", large: true, color: "rose" },
      { icon: "archive", label: "Archive", large: true, color: "green" }
    ]},
    { label: "Respond", items: [
      { icon: "reply", label: "Reply", large: true, color: "blue" },
      { icon: "replyAll", label: "Reply\nAll", large: true, color: "blue" },
      { icon: "forward", label: "Forward", large: true, color: "blue" }
    ]},
    { label: "Quick Steps", items: [
      { icon: "folder", label: "Move", large: true },
      { icon: "rules", label: "Rules", large: true, color: "violet" }
    ]},
    { label: "Tags", items: [
      { icon: "alert", label: "Mark Unread", small: true, color: "amber" },
      { icon: "category", label: "Categorize", small: true, color: "violet" },
      { icon: "flag", label: "Follow Up", small: true, color: "rose" }
    ]},
    { label: "Find", items: [
      { icon: "people", label: "Search\nPeople", large: true }
    ]}
  ],
  "Send/Receive": [
    { label: "Send & Receive", items: [
      { icon: "sync", label: "Send/Receive\nAll Folders", large: true, primary: true },
      { icon: "reload", label: "Update\nFolder", large: true, color: "blue" },
      { icon: "send", label: "Send All", large: true, color: "green" }
    ]},
    { label: "Preferences", items: [
      { icon: "offline", label: "Work\nOffline", large: true, color: "amber" },
      { icon: "cancel", label: "Cancel", small: true, color: "rose" },
      { icon: "progress", label: "Show Progress", small: true, color: "violet" }
    ]}
  ],
  View: [
    { label: "Layout", items: [
      { icon: "view", label: "Reading\nPane", large: true, color: "blue" },
      { icon: "layout", label: "Folder\nPane", large: true, color: "blue" },
      { icon: "reload", label: "Reset\nView", large: true }
    ]},
    { label: "Arrangement", items: [
      { icon: "arrange", label: "Sort By", small: true, color: "violet" },
      { icon: "filter", label: "Arrange By", small: true, color: "violet" },
      { icon: "density", label: "Layout Density", small: true, color: "green" }
    ]}
  ],
  Help: [
    { label: "Help", items: [
      { icon: "info", label: "About", large: true, color: "blue" },
      { icon: "settings", label: "Settings", large: true, primary: true }
    ]},
    { label: "Account", items: [
      { icon: "people", label: "Account\nSettings", large: true, color: "violet" },
      { icon: "download", label: "Check for\nUpdates", large: true, color: "green" }
    ]}
  ]
};

const TABS = ["Home", "Send/Receive", "View", "Help"];

function Ribbon({ activeTab, setActiveTab, ribbonStyle, setRibbonStyle, onFileTab, onAction }) {
  const items = (ribbonStyle === "simplified" ? SIMPLIFIED : CLASSIC)[activeTab] || [];

  return (
    <div className="ribbon">
      <div className="ribbon-tabs">
        <button className="ribbon-tab file-tab" onClick={onFileTab}>File</button>
        {TABS.map(tab => (
          <button
            key={tab}
            className={`ribbon-tab ${activeTab === tab ? "active" : ""}`}
            onClick={() => setActiveTab(tab)}
          >
            {tab}
          </button>
        ))}
        <div className="ribbon-toggle">
          <button
            className={ribbonStyle === "simplified" ? "active" : ""}
            onClick={() => setRibbonStyle("simplified")}
            title="Simplified ribbon"
          >
            <Icon name="minus" size={14} />
          </button>
          <button
            className={ribbonStyle === "classic" ? "active" : ""}
            onClick={() => setRibbonStyle("classic")}
            title="Classic ribbon"
          >
            <Icon name="density" size={14} />
          </button>
        </div>
      </div>
      {ribbonStyle === "simplified" ? (
        <div className="ribbon-content simplified">
          {items.map((it, i) => {
            if (it.kind === "div") return <div key={i} className="rb-divider" />;
            const cls = `rb-btn ${it.primary ? "primary" : ""} ${it.color ? "accent-" + it.color : ""}`;
            return (
              <button key={i} className={cls} onClick={() => onAction && onAction(it.label)}>
                <span className="rb-icn"><Icon name={it.icon} size={16} /></span>
                <span>{it.label}</span>
              </button>
            );
          })}
        </div>
      ) : (
        <div className="ribbon-content classic">
          {items.map((group, gi) => (
            <div key={gi} className="rb-group">
              <div className="rb-group-items">
                {/* Split into large buttons + a small-stack trailing */}
                {group.items.filter(x => x.large).map((it, i) => (
                  <button
                    key={i}
                    className={`rb-large ${it.primary ? "primary-tint" : ""} accent-${it.color || (it.primary ? "blue" : "")}`}
                    onClick={() => onAction && onAction(it.label.replace(/\n/g, " "))}
                    style={it.primary ? { color: "var(--accent)" } : undefined}
                  >
                    <span className="rb-icn" style={{ color: it.primary ? "var(--accent)" : (it.color === "green" ? "var(--green)" : it.color === "amber" ? "var(--amber)" : it.color === "rose" ? "var(--rose)" : it.color === "violet" ? "var(--violet)" : it.color === "blue" ? "var(--accent)" : "var(--text-secondary)") }}>
                      <Icon name={it.icon} size={22} />
                    </span>
                    <span className="rb-large-label">{it.label.split("\n").map((l, j) => <span key={j} style={{ display: "block" }}>{l}</span>)}</span>
                  </button>
                ))}
                {group.items.some(x => x.small) && (
                  <div className="rb-small-stack">
                    {group.items.filter(x => x.small).map((it, i) => (
                      <button key={i} className="rb-small" onClick={() => onAction && onAction(it.label)}>
                        <span style={{ color: it.color === "green" ? "var(--green)" : it.color === "amber" ? "var(--amber)" : it.color === "rose" ? "var(--rose)" : it.color === "violet" ? "var(--violet)" : "var(--accent)" }}>
                          <Icon name={it.icon} size={14} />
                        </span>
                        <span>{it.label}</span>
                      </button>
                    ))}
                  </div>
                )}
              </div>
              <div className="rb-group-label">{group.label}</div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

window.TitleBar = TitleBar;
window.Ribbon = Ribbon;
window.AvatarPopover = AvatarPopover;
