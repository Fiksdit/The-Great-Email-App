// data.jsx — App state, accounts, folders, sample emails (newsletter HTML).

const ACCOUNT_COLORS = ["#3A6FF8", "#14a37f", "#8a5cf5", "#d29014", "#d4406b", "#0ea5e9", "#f43f5e", "#22c55e"];

const ACCOUNTS = [
  {
    id: "primary",
    name: "Personal — me@somemail.io",
    short: "me@somemail.io",
    initials: "M",
    color: "#3A6FF8",
    status: "connected",
    primary: true,
    folders: [
      { id: "inbox", name: "Inbox", icon: "inbox", count: 12, special: true },
      { id: "drafts", name: "Drafts", icon: "drafts", count: 3 },
      { id: "sent", name: "Sent Items", icon: "sent" },
      { id: "deleted", name: "Deleted Items", icon: "trash", count: 87 },
      { id: "junk", name: "Junk", icon: "junk", count: 4 },
      { id: "archive", name: "Archive", icon: "archive" },
      { id: "newsletters", name: "Newsletters", icon: "folder", count: 24, nested: true },
      { id: "receipts", name: "Receipts", icon: "folder", nested: true },
      { id: "travel", name: "Travel", icon: "folder", count: 2, nested: true }
    ]
  },
  {
    id: "work",
    name: "Work — j.dixon@workmail.co",
    short: "j.dixon@workmail.co",
    initials: "JD",
    color: "#14a37f",
    status: "connected",
    folders: [
      { id: "inbox-work", name: "Inbox", icon: "inbox", count: 38, special: true },
      { id: "drafts-work", name: "Drafts", icon: "drafts" },
      { id: "sent-work", name: "Sent Items", icon: "sent" },
      { id: "deleted-work", name: "Deleted Items", icon: "trash" },
      { id: "junk-work", name: "Junk", icon: "junk" },
      { id: "archive-work", name: "Archive", icon: "archive" },
      { id: "clients", name: "Clients", icon: "folder", count: 7, nested: true },
      { id: "internal", name: "Internal", icon: "folder", nested: true }
    ]
  },
  {
    id: "side",
    name: "Studio — hello@brightside.studio",
    short: "hello@brightside.studio",
    initials: "BS",
    color: "#8a5cf5",
    status: "syncing",
    folders: [
      { id: "inbox-side", name: "Inbox", icon: "inbox", count: 5, special: true },
      { id: "drafts-side", name: "Drafts", icon: "drafts" },
      { id: "sent-side", name: "Sent Items", icon: "sent" },
      { id: "deleted-side", name: "Deleted Items", icon: "trash" }
    ]
  },
  {
    id: "alumni",
    name: "Alumni — j.dixon@alum.edu",
    short: "j.dixon@alum.edu",
    initials: "AL",
    color: "#d29014",
    status: "connected",
    folders: [
      { id: "inbox-alum", name: "Inbox", icon: "inbox", count: 1, special: true },
      { id: "sent-alum", name: "Sent Items", icon: "sent" },
      { id: "archive-alum", name: "Archive", icon: "archive" }
    ]
  },
  {
    id: "legacy",
    name: "Legacy IMAP — old.address@isp.net",
    short: "old.address@isp.net",
    initials: "L",
    color: "#d4406b",
    status: "error",
    folders: [
      { id: "inbox-legacy", name: "Inbox", icon: "inbox", count: 0, special: true },
      { id: "sent-legacy", name: "Sent", icon: "sent" }
    ]
  }
];

const EMAILS = [
  {
    id: "e1",
    group: "Today",
    sender: "Field Notes Weekly",
    senderEmail: "issue@fieldnotes.email",
    avatar: "FN",
    color: "#14a37f",
    subject: "Issue 142 — On craft, calm software, and the slow web",
    preview: "This week: the case for slower interfaces, three small tools that get out of your way, and a long read on workshop discipline by Aiko Tanaka.",
    time: "9:14 AM",
    fullTime: "Tue, Apr 28, 2026, 9:14 AM",
    unread: true,
    flagged: true,
    important: false,
    attachments: [
      { name: "issue-142.pdf", size: "1.2 MB", color: "#d4406b", ext: "PDF" },
      { name: "cover-art.jpg", size: "640 KB", color: "#3A6FF8", ext: "JPG" }
    ],
    selected: true,
    type: "newsletter",
    to: "you <me@somemail.io>",
    cc: "subscribers <subs@fieldnotes.email>"
  },
  {
    id: "e2",
    group: "Today",
    sender: "Priya Anand",
    senderEmail: "priya@workmail.co",
    avatar: "PA",
    color: "#8a5cf5",
    subject: "Re: Q2 roadmap review — quick thoughts before Friday",
    preview: "Hey — looked through the deck last night. Two things stood out. First, the timeline for the auth migration feels aggressive given we're still scoping…",
    time: "8:52 AM",
    unread: true,
    important: true,
    attachments: []
  },
  {
    id: "e3",
    group: "Today",
    sender: "GitHub",
    senderEmail: "noreply@github.com",
    avatar: "GH",
    color: "#1a1a1a",
    subject: "[tgea/desktop] PR #284: Fluent ribbon polish (review requested)",
    preview: "@you, Marco Reyes requested your review on this pull request. 14 files changed (+412 −188) — most edits in src/ribbon/, src/theme/, and src/icons/.",
    time: "8:04 AM",
    unread: false,
    attachments: []
  },
  {
    id: "e4",
    group: "Today",
    sender: "Calendar",
    senderEmail: "calendar@somemail.io",
    avatar: "CA",
    color: "#3A6FF8",
    subject: "Reminder: Design crit — Thursday 2pm",
    preview: "Heads up: you have Design crit (Thu, May 1, 2:00 PM – 3:00 PM) starting in 2 days. Location: Room 4 / Zoom link in description.",
    time: "7:30 AM",
    unread: false,
    attachments: []
  },
  {
    id: "e5",
    group: "Yesterday",
    sender: "Mira Okonkwo",
    senderEmail: "mira@brightside.studio",
    avatar: "MO",
    color: "#0ea5e9",
    subject: "Hand-off: brand guidelines + final asset bundle",
    preview: "Final files are in the shared drive. I've attached the lighter version of the brand guidelines as a quick reference. Let me know if you want me to walk through anything before…",
    time: "Yesterday",
    unread: false,
    flagged: true,
    attachments: [{ name: "brand-guidelines.pdf", size: "8.4 MB", ext: "PDF" }]
  },
  {
    id: "e6",
    group: "Yesterday",
    sender: "Stripe",
    senderEmail: "receipts@stripe.com",
    avatar: "S",
    color: "#635bff",
    subject: "Your receipt from Brightside Studio LLC",
    preview: "Thanks for your business. Your payment of $2,400.00 USD to Brightside Studio LLC has been completed. View your invoice or manage your subscription…",
    time: "Yesterday",
    unread: false,
    attachments: []
  },
  {
    id: "e7",
    group: "Yesterday",
    sender: "Jules Park",
    senderEmail: "jules@workmail.co",
    avatar: "JP",
    color: "#f43f5e",
    subject: "Lunch this week?",
    preview: "Free Thursday or Friday? I'm in the office both days and would love to catch up properly — feels like ages. The new place on 4th finally opened btw…",
    time: "Yesterday",
    unread: false,
    attachments: []
  },
  {
    id: "e8",
    group: "Last Week",
    sender: "1Password",
    senderEmail: "noreply@1password.com",
    avatar: "1P",
    color: "#1d4ed8",
    subject: "Your monthly Watchtower report — 2 items need attention",
    preview: "We checked your vaults and found 2 items that may need a closer look: 1 reused password, 1 site that supports 2FA you haven't enabled yet.",
    time: "Apr 23",
    unread: false,
    attachments: []
  },
  {
    id: "e9",
    group: "Last Week",
    sender: "Ben Carver",
    senderEmail: "ben@oldfriend.dev",
    avatar: "BC",
    color: "#22c55e",
    subject: "weird thing in the new build",
    preview: "Hey — was poking around the latest nightly and noticed the sync status indicator gets stuck on amber after a manual reconnect. Repro: disable wifi for ~30s, re-enable…",
    time: "Apr 22",
    unread: false,
    attachments: [{ name: "screen-recording.mov", size: "12 MB", ext: "MOV" }]
  },
  {
    id: "e10",
    group: "Last Week",
    sender: "Aiko Tanaka",
    senderEmail: "aiko@fieldnotes.email",
    avatar: "AT",
    color: "#14a37f",
    subject: "Workshop visit — would you be up for a feature?",
    preview: "Big fan of what you've been making. We're putting together a series on independent software workshops and would love to feature TGEA in an upcoming issue…",
    time: "Apr 21",
    unread: false,
    important: true,
    attachments: []
  }
];

window.ACCOUNTS = ACCOUNTS;
window.EMAILS = EMAILS;
window.ACCOUNT_COLORS = ACCOUNT_COLORS;
