// modals.jsx — File backstage, Add Account dialog, Settings, First-run sign-in

const { useState: useStateM } = React;

function Backstage({ tab = "Info", setTab, onClose, onOpenSettings, onOpenAddAccount, onSignOut }) {
  const items = [
    { id: "Info", icon: "info", label: "Account Info" },
    { id: "Open", icon: "folderOpen", label: "Open & Export" },
    { id: "Print", icon: "download", label: "Save As" },
    { id: "Rules", icon: "rules", label: "Manage Rules" },
    { id: "Account", icon: "people", label: "Account Settings" },
    { id: "Options", icon: "settings", label: "Options" },
    { id: "Sync", icon: "sync", label: "Settings Sync" },
    { id: "About", icon: "info", label: "About TGEA" }
  ];

  return (
    <div className="backstage">
      <div className="backstage-rail">
        <button className="backstage-back" onClick={onClose}><Icon name="chevron" size={14} style={{ transform: "rotate(180deg)" }} /></button>
        {items.map(it => (
          <button
            key={it.id}
            className={`backstage-rail-item ${tab === it.id ? "active" : ""}`}
            onClick={() => setTab(it.id)}
          >
            <Icon name={it.icon} size={16} />
            <span>{it.label}</span>
          </button>
        ))}
      </div>
      <div className="backstage-content">
        {tab === "Info" && (
          <>
            <h1>Account Information</h1>
            <p style={{ color: "var(--text-tertiary)", margin: "0 0 22px", fontSize: 13 }}>
              Connected IMAP accounts and their sync status.
            </p>
            {ACCOUNTS.map(a => (
              <div key={a.id} className="backstage-section">
                <div className="backstage-account-card">
                  <div className="acct-mark-lg" style={{ background: a.color }}>{a.initials}</div>
                  <div className="acct-info">
                    <b>{a.short}</b>
                    <span>IMAP/SMTP · {a.status === "connected" ? "Connected" : a.status === "syncing" ? "Syncing…" : "Connection error"}</span>
                  </div>
                  <span className={`account-status ${a.status}`} style={{ width: 10, height: 10 }} />
                  <button className="btn">Account Settings</button>
                </div>
              </div>
            ))}
            <button className="btn primary" onClick={onOpenAddAccount} style={{ marginTop: 8 }}>
              <Icon name="plus" size={14} /> Add Account
            </button>
          </>
        )}
        {tab === "Sync" && (
          <>
            <h1>Settings Sync</h1>
            <p style={{ color: "var(--text-tertiary)", margin: "0 0 22px", fontSize: 13 }}>
              Sign in with Google to back up your account configuration (not passwords) across devices.
            </p>
            <div className="backstage-section">
              <div style={{ display: "flex", alignItems: "center", gap: 14, marginBottom: 14 }}>
                <Icon name="google" size={36} />
                <div style={{ flex: 1 }}>
                  <b style={{ fontSize: 14 }}>Signed in as Morgan Dixon</b>
                  <div style={{ fontSize: 12, color: "var(--text-tertiary)" }}>m.dixon@gmail.com · Last sync 4 minutes ago</div>
                </div>
                <button className="btn" onClick={onSignOut}>Sign out</button>
              </div>
              <div className="settings-row" style={{ borderBottom: 0, padding: "8px 0 0" }}>
                <div className="settings-row-label">
                  <b>Sync account configuration</b>
                  <span>IMAP/SMTP host, ports, display names — never passwords.</span>
                </div>
                <label className="switch"><input type="checkbox" defaultChecked /><span className="switch-track" /></label>
              </div>
              <div className="settings-row" style={{ borderBottom: 0, padding: "8px 0 0" }}>
                <div className="settings-row-label">
                  <b>Sync rules and folders</b>
                  <span>Filter rules, folder hierarchy, signatures.</span>
                </div>
                <label className="switch"><input type="checkbox" defaultChecked /><span className="switch-track" /></label>
              </div>
              <div className="settings-row" style={{ borderBottom: 0, padding: "8px 0 0" }}>
                <div className="settings-row-label">
                  <b>Sync appearance</b>
                  <span>Theme, accent color, density.</span>
                </div>
                <label className="switch"><input type="checkbox" /><span className="switch-track" /></label>
              </div>
            </div>
          </>
        )}
        {tab === "About" && (
          <>
            <h1>About TGEA</h1>
            <div className="backstage-section" style={{ display: "flex", gap: 18, alignItems: "center" }}>
              <div style={{
                width: 64, height: 64, borderRadius: 14,
                background: "linear-gradient(135deg, var(--accent), var(--green))",
                display: "grid", placeItems: "center", color: "white", fontSize: 28, fontWeight: 700
              }}>T</div>
              <div>
                <b style={{ fontSize: 16 }}>The Great Email App</b>
                <div style={{ fontSize: 12, color: "var(--text-tertiary)", marginTop: 4 }}>Version 1.0.0 (build 2026.04.28) · WPF · .NET 8</div>
                <div style={{ fontSize: 12, color: "var(--text-tertiary)" }}>© 2026 — Distributed under MIT.</div>
              </div>
            </div>
          </>
        )}
        {!["Info","Sync","About"].includes(tab) && (
          <>
            <h1>{items.find(i => i.id === tab)?.label}</h1>
            <p style={{ color: "var(--text-tertiary)" }}>Section content placeholder.</p>
            {tab === "Options" && (
              <button className="btn primary" onClick={onOpenSettings}><Icon name="settings" size={14} /> Open Settings dialog</button>
            )}
          </>
        )}
      </div>
    </div>
  );
}

function AddAccountModal({ onClose }) {
  const [tested, setTested] = useStateM(null);
  const test = () => {
    setTested("testing");
    setTimeout(() => setTested("ok"), 1100);
  };
  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" style={{ width: 580 }} onClick={e => e.stopPropagation()}>
        <div className="modal-header">
          <h2 className="modal-title">Add IMAP Account</h2>
          <button className="modal-close" onClick={onClose}><Icon name="close" size={14} /></button>
        </div>
        <div className="modal-body">
          <p style={{ margin: "0 0 16px", fontSize: 12.5, color: "var(--text-tertiary)" }}>
            Configure an incoming IMAP server and outgoing SMTP server. Test the connection before saving.
          </p>
          <div className="field-grid">
            <div className="field span-2">
              <label>Display name</label>
              <input className="input" defaultValue="Morgan Dixon" />
            </div>
            <div className="field span-2">
              <label>Email address</label>
              <input className="input" defaultValue="morgan@somemail.io" />
            </div>

            <div className="field span-2" style={{ marginTop: 8 }}>
              <div style={{ fontSize: 11, fontWeight: 700, textTransform: "uppercase", letterSpacing: ".06em", color: "var(--text-tertiary)" }}>Incoming · IMAP</div>
            </div>
            <div className="field"><label>IMAP server</label><input className="input" defaultValue="imap.somemail.io" /></div>
            <div className="field"><label>Port</label><input className="input" defaultValue="993" /></div>
            <div className="field"><label>Encryption</label>
              <select className="select" defaultValue="SSL/TLS">
                <option>SSL/TLS</option><option>STARTTLS</option><option>None</option>
              </select>
            </div>
            <div className="field"><label>Username</label><input className="input" defaultValue="morgan@somemail.io" /></div>
            <div className="field span-2"><label>Password</label><input className="input" type="password" defaultValue="••••••••••••" /></div>

            <div className="field span-2" style={{ marginTop: 8 }}>
              <div style={{ fontSize: 11, fontWeight: 700, textTransform: "uppercase", letterSpacing: ".06em", color: "var(--text-tertiary)" }}>Outgoing · SMTP</div>
            </div>
            <div className="field"><label>SMTP server</label><input className="input" defaultValue="smtp.somemail.io" /></div>
            <div className="field"><label>Port</label><input className="input" defaultValue="465" /></div>
            <div className="field span-2"><label>Encryption</label>
              <select className="select" defaultValue="SSL/TLS">
                <option>SSL/TLS</option><option>STARTTLS</option><option>None</option>
              </select>
            </div>

            <div className="field span-2" style={{
              marginTop: 8, padding: "12px 14px", background: "var(--bg-pane-2)",
              border: "1px solid var(--border)", borderRadius: 8,
              flexDirection: "row", alignItems: "center", justifyContent: "space-between"
            }}>
              <div>
                <b style={{ fontSize: 12.5, display: "block" }}>Sync this account's settings via Google</b>
                <span style={{ fontSize: 11.5, color: "var(--text-tertiary)" }}>Server config syncs across devices. Passwords stay local.</span>
              </div>
              <label className="switch"><input type="checkbox" defaultChecked /><span className="switch-track" /></label>
            </div>
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn ghost" onClick={onClose}>Cancel</button>
          <button className="btn" onClick={test}>
            {tested === "testing" && <><Icon name="progress" size={13} /> Testing…</>}
            {tested === "ok" && <span style={{ color: "var(--green)", display: "inline-flex", alignItems: "center", gap: 6 }}><Icon name="check" size={13} /> Connection OK</span>}
            {!tested && <><Icon name="sync" size={13} /> Test Connection</>}
          </button>
          <button className="btn primary"><Icon name="check" size={13} /> Add Account</button>
        </div>
      </div>
    </div>
  );
}

function SettingsModal({ onClose, theme, setTheme, accent, setAccent, density, setDensity, ribbonStyle, setRibbonStyle }) {
  const [tab, setTab] = useStateM("Appearance");
  const tabs = [
    { id: "General", icon: "settings" },
    { id: "Accounts", icon: "people" },
    { id: "Appearance", icon: "paint" },
    { id: "Sync", icon: "sync" },
    { id: "Notifications", icon: "bell" },
    { id: "Advanced", icon: "rules" }
  ];

  const accents = ["#3A6FF8", "#14a37f", "#8a5cf5", "#0ea5e9", "#d29014", "#d4406b"];

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" style={{ width: 760 }} onClick={e => e.stopPropagation()}>
        <div className="modal-header">
          <h2 className="modal-title">Settings</h2>
          <button className="modal-close" onClick={onClose}><Icon name="close" size={14} /></button>
        </div>
        <div className="settings-layout">
          <div className="settings-rail">
            {tabs.map(t => (
              <button key={t.id} className={`settings-rail-item ${tab === t.id ? "active" : ""}`} onClick={() => setTab(t.id)}>
                <Icon name={t.icon} size={15} />
                {t.id}
              </button>
            ))}
          </div>
          <div className="settings-content">
            {tab === "Appearance" && (
              <>
                <h2>Appearance</h2>
                <div className="settings-row">
                  <div className="settings-row-label"><b>Theme</b><span>Match system or pick manually.</span></div>
                  <div className="seg">
                    {["light", "dark", "system"].map(t => (
                      <button key={t} className={theme === t ? "active" : ""} onClick={() => setTheme(t)}>{t[0].toUpperCase()+t.slice(1)}</button>
                    ))}
                  </div>
                </div>
                <div className="settings-row">
                  <div className="settings-row-label"><b>Accent color</b><span>Buttons, selection, links.</span></div>
                  <div className="swatch-row">
                    {accents.map(c => (
                      <button key={c} className={`swatch ${accent === c ? "active" : ""}`} style={{ background: c }} onClick={() => setAccent(c)} />
                    ))}
                  </div>
                </div>
                <div className="settings-row">
                  <div className="settings-row-label"><b>Density</b><span>How much breathing room in the message list.</span></div>
                  <div className="seg">
                    {["Compact", "Cozy", "Comfortable"].map(d => (
                      <button key={d} className={density === d ? "active" : ""} onClick={() => setDensity(d)}>{d}</button>
                    ))}
                  </div>
                </div>
                <div className="settings-row">
                  <div className="settings-row-label"><b>Ribbon style</b><span>Simplified is a single row; classic shows labeled groups.</span></div>
                  <div className="seg">
                    {["simplified", "classic"].map(r => (
                      <button key={r} className={ribbonStyle === r ? "active" : ""} onClick={() => setRibbonStyle(r)}>{r[0].toUpperCase()+r.slice(1)}</button>
                    ))}
                  </div>
                </div>
                <div className="settings-row">
                  <div className="settings-row-label"><b>Translucency</b><span>Mica-style background tint behind chrome.</span></div>
                  <label className="switch"><input type="checkbox" defaultChecked /><span className="switch-track" /></label>
                </div>
                <div className="settings-row">
                  <div className="settings-row-label"><b>Font size</b><span>Affects message list and reading pane body.</span></div>
                  <div className="seg">
                    {["S", "M", "L", "XL"].map(s => (
                      <button key={s} className={s === "M" ? "active" : ""}>{s}</button>
                    ))}
                  </div>
                </div>
              </>
            )}
            {tab !== "Appearance" && (
              <>
                <h2>{tab}</h2>
                <p style={{ color: "var(--text-tertiary)", fontSize: 13 }}>Settings for {tab.toLowerCase()} would appear here.</p>
                <div style={{
                  height: 220, border: "1px dashed var(--border-strong)", borderRadius: 10,
                  display: "grid", placeItems: "center", color: "var(--text-tertiary)", fontSize: 12,
                  fontFamily: "var(--font-mono)"
                }}>
                  {tab.toLowerCase()} placeholder
                </div>
              </>
            )}
          </div>
        </div>
        <div className="modal-footer">
          <button className="btn ghost" onClick={onClose}>Close</button>
          <button className="btn primary" onClick={onClose}>Done</button>
        </div>
      </div>
    </div>
  );
}

function SignInScreen({ onContinue, onSkip }) {
  return (
    <div className="signin-bg">
      <div className="signin-card">
        <div className="signin-mark">T</div>
        <h1>Welcome to The Great Email App</h1>
        <p>A calm, native-Windows IMAP client. Sign in with Google to back up your account configuration and preferences across devices — passwords stay local.</p>
        <button className="signin-google" onClick={onContinue}>
          <Icon name="google" size={20} />
          Sign in with Google to sync your settings
        </button>
        <div className="signin-skip" onClick={onSkip}>Skip — use locally only</div>
        <div style={{
          marginTop: 36, paddingTop: 20, borderTop: "1px solid var(--divider)",
          display: "flex", justifyContent: "center", gap: 22, fontSize: 11.5, color: "var(--text-tertiary)"
        }}>
          <span><Icon name="check" size={11} style={{ verticalAlign: "middle", color: "var(--green)" }} /> End-to-end IMAP</span>
          <span><Icon name="check" size={11} style={{ verticalAlign: "middle", color: "var(--green)" }} /> Local-first</span>
          <span><Icon name="check" size={11} style={{ verticalAlign: "middle", color: "var(--green)" }} /> Open source</span>
        </div>
      </div>
    </div>
  );
}

window.Backstage = Backstage;
window.AddAccountModal = AddAccountModal;
window.SettingsModal = SettingsModal;
window.SignInScreen = SignInScreen;
