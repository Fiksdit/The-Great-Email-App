// icons.jsx — Fluent-style line icons (original, stroke-based, 1.5px)
// Exported on window so other Babel scripts can use them.

const Icon = ({ name, size = 16, className, style }) => {
  const paths = ICONS[name];
  if (!paths) return null;
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      width={size}
      height={size}
      viewBox="0 0 20 20"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      style={style}
      aria-hidden="true"
    >
      {paths}
    </svg>
  );
};

// Each entry: array of <path/> or other JSX SVG primitives.
const ICONS = {
  // chrome
  search: [<circle key="c" cx="9" cy="9" r="5" />, <path key="l" d="M13 13l3.5 3.5" />],
  chevron: [<path key="p" d="M7 4l6 6-6 6" />],
  chevronDown: [<path key="p" d="M5 7l5 5 5-5" />],
  chevronUp: [<path key="p" d="M5 13l5-5 5 5" />],
  close: [<path key="a" d="M5 5l10 10" />, <path key="b" d="M15 5L5 15" />],
  minimize: [<path key="p" d="M5 10h10" />],
  maximize: [<rect key="r" x="5" y="5" width="10" height="10" rx="1" />],
  more: [<circle key="a" cx="5" cy="10" r="1" />, <circle key="b" cx="10" cy="10" r="1" />, <circle key="c" cx="15" cy="10" r="1" />],
  plus: [<path key="a" d="M10 4v12" />, <path key="b" d="M4 10h12" />],
  minus: [<path key="p" d="M4 10h12" />],
  settings: [
    <circle key="c" cx="10" cy="10" r="2.2" />,
    <path key="p" d="M10 2.5v2M10 15.5v2M2.5 10h2M15.5 10h2M4.7 4.7l1.4 1.4M13.9 13.9l1.4 1.4M4.7 15.3l1.4-1.4M13.9 6.1l1.4-1.4" />
  ],
  info: [<circle key="c" cx="10" cy="10" r="7" />, <path key="i" d="M10 9v4.5M10 6.5v.5" />],
  bell: [
    <path key="b" d="M5 13.5V9a5 5 0 1 1 10 0v4.5" />,
    <path key="r" d="M3.5 14.5h13" />,
    <path key="c" d="M8 16.5a2 2 0 0 0 4 0" />
  ],
  // mail actions
  inbox: [
    <path key="r" d="M3 10v5a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2v-5" />,
    <path key="t" d="M3 10l1.5-5A2 2 0 0 1 6.4 3.5h7.2A2 2 0 0 1 15.5 5L17 10" />,
    <path key="d" d="M3 10h3.5l1.2 2h4.6l1.2-2H17" />
  ],
  send: [<path key="a" d="M3 10l14-7-5 14-2.5-5L3 10z" />],
  reply: [<path key="r" d="M9 5L4 10l5 5" />, <path key="a" d="M4 10h7a5 5 0 0 1 5 5v1" />],
  replyAll: [
    <path key="r1" d="M6 5L1.5 10 6 15" />,
    <path key="r2" d="M11 5L6.5 10 11 15" />,
    <path key="a" d="M6.5 10h6a5 5 0 0 1 5 5v1" />
  ],
  forward: [<path key="r" d="M11 5l5 5-5 5" />, <path key="a" d="M16 10H9a5 5 0 0 0-5 5v1" />],
  archive: [
    <rect key="t" x="3" y="4" width="14" height="3.5" rx="1" />,
    <path key="b" d="M4 7.5V15a1.5 1.5 0 0 0 1.5 1.5h9A1.5 1.5 0 0 0 16 15V7.5" />,
    <path key="h" d="M8 11h4" />
  ],
  trash: [
    <path key="t" d="M3.5 6h13" />,
    <path key="b" d="M5 6l1 9.5A1.5 1.5 0 0 0 7.5 17h5A1.5 1.5 0 0 0 14 15.5L15 6" />,
    <path key="h" d="M7.5 6V4.5A1.5 1.5 0 0 1 9 3h2a1.5 1.5 0 0 1 1.5 1.5V6" />
  ],
  flag: [<path key="p" d="M5 3v14M5 4h9l-1.5 3.5L14 11H5" />],
  flagFilled: [<path key="p" d="M5 3v14" />, <path key="f" d="M5 4h9l-1.5 3.5L14 11H5z" fill="currentColor" />],
  attach: [<path key="p" d="M14 6.5l-6 6a2.5 2.5 0 0 1-3.5-3.5l7-7a4 4 0 0 1 5.5 5.5l-7 7" />],
  star: [<path key="p" d="M10 3l2.2 4.6 5 .7-3.6 3.6.9 5L10 14.5 5.5 17l.9-5L2.8 8.3l5-.7L10 3z" />],
  starFilled: [<path key="p" d="M10 3l2.2 4.6 5 .7-3.6 3.6.9 5L10 14.5 5.5 17l.9-5L2.8 8.3l5-.7L10 3z" fill="currentColor" />],
  folder: [<path key="p" d="M3 6.5A1.5 1.5 0 0 1 4.5 5h3l1.5 2h6.5A1.5 1.5 0 0 1 17 8.5v6A1.5 1.5 0 0 1 15.5 16h-11A1.5 1.5 0 0 1 3 14.5v-8z" />],
  folderOpen: [
    <path key="p" d="M3 14V6.5A1.5 1.5 0 0 1 4.5 5h3l1.5 2h6.5A1.5 1.5 0 0 1 17 8.5V9" />,
    <path key="o" d="M3 14l2-5h13l-2 5a1.5 1.5 0 0 1-1.4 1H4.4A1.5 1.5 0 0 1 3 14z" />
  ],
  drafts: [<path key="p" d="M4 4h8l4 4v8H4z" />, <path key="d" d="M12 4v4h4" />, <path key="l" d="M7 11h6M7 13.5h4" />],
  sent: [<path key="a" d="M3 10l14-7-5 14-2.5-5L3 10z" />],
  junk: [<circle key="c" cx="10" cy="10" r="7" />, <path key="x1" d="M7 7l6 6" />, <path key="x2" d="M13 7l-6 6" />],
  newMail: [
    <path key="m" d="M3 6.5l7 4.5 7-4.5" />,
    <rect key="r" x="3" y="5" width="14" height="10" rx="1.5" />,
    <circle key="c" cx="16" cy="5" r="3" fill="white" stroke="currentColor" />,
    <path key="p1" d="M16 3.5v3M14.5 5h3" />
  ],
  reload: [
    <path key="a" d="M16 5v4h-4" />,
    <path key="c" d="M16 9a6.5 6.5 0 1 0-1.5 4.5" />
  ],
  filter: [<path key="p" d="M3 5h14l-5 6.5V16l-4-1.5V11.5L3 5z" />],
  paperclip: [<path key="p" d="M14 6.5l-6 6a2.5 2.5 0 0 1-3.5-3.5l7-7a4 4 0 0 1 5.5 5.5l-7 7" />],
  alert: [<path key="t" d="M10 3l8 14H2L10 3z" />, <path key="i" d="M10 8.5v3.5M10 14.5v.5" />],
  category: [
    <rect key="a" x="3" y="3" width="6" height="6" rx="1" fill="currentColor" opacity=".4" />,
    <rect key="b" x="11" y="3" width="6" height="6" rx="1" fill="currentColor" opacity=".7" />,
    <rect key="c" x="3" y="11" width="6" height="6" rx="1" fill="currentColor" opacity=".9" />,
    <rect key="d" x="11" y="11" width="6" height="6" rx="1" fill="currentColor" opacity=".5" />
  ],
  rules: [<path key="a" d="M4 5h12M4 10h8M4 15h12" />, <circle key="b" cx="14" cy="10" r="2" fill="white" stroke="currentColor" />],
  people: [
    <circle key="a" cx="7" cy="7" r="2.5" />,
    <circle key="b" cx="13.5" cy="8" r="2" />,
    <path key="c" d="M3 16c.5-2.5 2.2-4 4-4s3.5 1.5 4 4" />,
    <path key="d" d="M11.5 16c.4-1.8 1.5-3 3-3s2.6 1.2 3 3" />
  ],
  view: [<circle key="c" cx="10" cy="10" r="2.5" />, <path key="e" d="M2 10s3-5 8-5 8 5 8 5-3 5-8 5-8-5-8-5z" />],
  layout: [
    <rect key="a" x="3" y="3" width="14" height="14" rx="1.5" />,
    <path key="b" d="M9 3v14M3 8h6" />
  ],
  density: [<path key="p" d="M3 5h14M3 10h14M3 15h14" />],
  arrange: [<path key="a" d="M5 6h10M7 10h6M9 14h2" />],
  globe: [<circle key="c" cx="10" cy="10" r="7" />, <path key="a" d="M3 10h14M10 3a10 10 0 0 1 0 14M10 3a10 10 0 0 0 0 14" />],
  offline: [<path key="c" d="M3 12a8 8 0 0 1 14-3" />, <path key="x" d="M3 3l14 14" />],
  cancel: [<circle key="c" cx="10" cy="10" r="7" />, <path key="l" d="M5.5 5.5l9 9" />],
  progress: [<circle key="c" cx="10" cy="10" r="7" strokeDasharray="14 6" />],
  google: [
    <path key="g" d="M17 10.2c0-.6-.05-1.2-.15-1.7H10v3.3h3.9a3.3 3.3 0 0 1-1.4 2.2v1.8h2.3c1.4-1.2 2.2-3.1 2.2-5.6z" fill="#4285F4" stroke="none" />,
    <path key="g2" d="M10 17c1.9 0 3.6-.6 4.8-1.7l-2.3-1.8c-.6.4-1.5.7-2.5.7-1.9 0-3.6-1.3-4.2-3H3.4v1.9A7 7 0 0 0 10 17z" fill="#34A853" stroke="none" />,
    <path key="g3" d="M5.8 11.2A4.2 4.2 0 0 1 5.6 10c0-.4.1-.8.2-1.2V6.9H3.4A7 7 0 0 0 3 10c0 1.1.3 2.2.8 3.1l2.4-1.9z" fill="#FBBC05" stroke="none" />,
    <path key="g4" d="M10 5.6c1 0 2 .4 2.7 1.1l2-2A7 7 0 0 0 10 3a7 7 0 0 0-6.6 3.9l2.4 1.9C6.4 6.9 8.1 5.6 10 5.6z" fill="#EA4335" stroke="none" />
  ],
  signOut: [<path key="d" d="M11 5h-5a1 1 0 0 0-1 1v8a1 1 0 0 0 1 1h5" />, <path key="a" d="M9 10h8M14 7l3 3-3 3" />],
  check: [<path key="p" d="M4 10.5l3.5 3.5L16 5.5" />],
  sync: [<path key="a" d="M3 10a7 7 0 0 1 12-5l1.5 1.5" />, <path key="b" d="M17 10a7 7 0 0 1-12 5l-1.5-1.5" />, <path key="c" d="M16 3v3.5h-3.5M4 17v-3.5H7.5" />],
  zoomIn: [<circle key="c" cx="9" cy="9" r="5" />, <path key="l" d="M13 13l3 3M9 7v4M7 9h4" />],
  zoomOut: [<circle key="c" cx="9" cy="9" r="5" />, <path key="l" d="M13 13l3 3M7 9h4" />],
  upload: [<path key="a" d="M10 14V4M5 9l5-5 5 5" />, <path key="b" d="M3 16h14" />],
  download: [<path key="a" d="M10 4v10M5 9l5 5 5-5" />, <path key="b" d="M3 16h14" />],
  bold: [<path key="p" d="M5 4h5a3 3 0 0 1 0 6H5z" />, <path key="q" d="M5 10h6a3 3 0 0 1 0 6H5z" />],
  pin: [<path key="p" d="M11 3l6 6-2 2-2-1-3 3-1 4-1-1-3-3-1-1 4-1 3-3-1-2 2-2z" />],
  contact: [<circle key="h" cx="10" cy="7.5" r="3" />, <path key="b" d="M3 17c1-3.5 4-5.5 7-5.5s6 2 7 5.5" />],
  link: [<path key="a" d="M9 11l-3 3a2.5 2.5 0 0 1-3.5-3.5l3-3" />, <path key="b" d="M11 9l3-3a2.5 2.5 0 0 1 3.5 3.5l-3 3" />, <path key="c" d="M7.5 12.5l5-5" />],
  paint: [<path key="b" d="M16 7L8.5 14.5l-2 2-3 1 1-3 2-2L14 5z" />, <path key="t" d="M14 5l3 3" />],
  sparkle: [<path key="p" d="M10 3v3M10 14v3M3 10h3M14 10h3M5.5 5.5l2 2M12.5 12.5l2 2M14.5 5.5l-2 2M7.5 12.5l-2 2" />]
};

window.Icon = Icon;
window.ICONS = ICONS;
