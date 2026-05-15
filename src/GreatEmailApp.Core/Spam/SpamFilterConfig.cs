// FILE: src/GreatEmailApp.Core/Spam/SpamFilterConfig.cs
// Created: 2026-05-15 | Revised: 2026-05-15 | Rev: 4
// Changed by: Claude Opus 4.7 on behalf of James Reed

namespace GreatEmailApp.Core.Spam;

/// <summary>
/// Persisted to %LOCALAPPDATA%\GreatEmailApp\spam-filter.json. Syncs via the
/// same Firestore pipeline as the rules store so block/trust lists travel
/// between PCs. NOT to be confused with AppSettings.ShowJunkUnreadBadge —
/// that's a UI concern; this is filter behavior.
/// </summary>
public sealed class SpamFilterConfig
{
    /// <summary>Master switch. False = engine is wired but classifies nothing.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>0–100. Messages scoring at or above this get moved to Junk.</summary>
    public int ThresholdScore { get; set; } = 70;

    /// <summary>
    /// Phrases that, if found in subject or preview, contribute to the score
    /// (subject hit = +40, preview hit = +25). Case-insensitive substring match.
    /// User can edit via Settings → Spam (future Phase 2).
    /// </summary>
    public List<string> SubjectKeywords { get; set; } = new()
    {
        // Pharma spam
        "viagra", "cialis", "levitra",
        // Lottery / 419
        "lottery winner", "claim your prize", "you have won", "nigerian prince",
        "inheritance fund", "beneficiary of",
        // BEC / wire-fraud
        "wire transfer urgent", "urgent payment required", "transfer funds immediately",
        // Crypto scams
        "crypto airdrop", "free bitcoin", "free ethereum", "double your crypto",
        // Health spam
        "weight loss miracle", "lose weight fast", "miracle cure",
        // MLM / work-from-home
        "work from home", "make money fast", "earn $$$",
        // Phishing
        "account suspended", "verify your account", "password expired",
        "click here to verify", "urgent action required", "account locked",
        "unusual sign-in activity",
        // Replicas
        "replica watches", "replica handbags",
        // Adult / gambling
        "casino bonus", "gambling winnings",
        // Financial / investor cold-outreach. The app owner does not receive
        // banking or investment correspondence at this address, so any of these
        // patterns is high-signal spam. Each is rare in legitimate mail. Two
        // hits in the same message reliably clears the default 70 threshold.
        "family office", "actively investing", "growth equity", "private equity firm",
        "this week or next", "regarding your business", "discuss your business",
        "portfolio companies", "deal flow", "managing director, investments",
        "investor relations", "venture capital firm", "buy-side opportunity",
        "interested in your company",
        // Business-acquisition cold outreach ("we represent buyers who want to
        // purchase your small business"). Patterns from the GoExio / Doug
        // Miller class of email — strong specific phrases, rare in legit mail.
        "qualified buyers", "considered selling", "buyers are paying",
        "worth a private chat", "zero obligation", "what buyers are paying",
        "ebitda", "ask us to find", "deals closed at",
        // Overseas-manufacturer cold outreach (ITZR-style). Mostly Chinese
        // contract manufacturers cold-pitching laptops/PCBs/SMT services.
        "iso-compliant", "smt lines", "low moq", "monthly capacity",
        "could we arrange a brief discussion", "convenient time",
        "tailored to specific requirements", "global clients",
        // Phishing / fake-corporate notification language.
        "complete the process at", "verify your identity", "view request",
        "respond to a line of credit",
        // Mailbox-quota / IT-impersonation phishing. Pretends to be from
        // the user's own mail provider ("your mailbox needs attention,
        // click here to avoid suspension"). The user's mailbox admin is
        // their own domain — these never come from outside legitimately.
        "mailbox requires attention", "mailbox status", "resolve mailbox",
        "update your mailbox", "verify your mailbox", "mailbox quota",
        "messages may be temporarily held", "mailbox suspended",
        "use the link below", "to avoid suspension", "to avoid interruption",
        "kindly verify",
    };

    /// <summary>
    /// Built-in default keyword list. Used by the config store to merge new
    /// shipping defaults into an existing user file so users upgrading don't
    /// miss freshly-added patterns. Kept in sync with the field initializer
    /// above — if you add a keyword there, add it here too. Phase 2 will
    /// track user-removed defaults so this merge can be smarter.
    /// </summary>
    public static IReadOnlyList<string> BuiltInKeywords => new SpamFilterConfig().SubjectKeywords;

    /// <summary>
    /// Sender email addresses (exact match) or domains (leading "@", e.g.
    /// "@spam-domain.tk") that are ALWAYS classified as spam.
    /// </summary>
    public List<string> BlockedSenders { get; set; } = new();

    /// <summary>
    /// Sender email addresses or domains that are NEVER classified as spam.
    /// Bootstrapped from each account's Sent folder on first run (anyone the
    /// user has emailed becomes trusted). User can edit via Settings → Spam.
    /// </summary>
    public List<string> TrustedSenders { get; set; } = new();

    /// <summary>
    /// One-shot flag: false until SpamFilterEngine has finished its first-run
    /// Sent-folder scan to seed TrustedSenders. Avoids rescanning on every
    /// launch and avoids burying the user's manual edits if they cleared the
    /// list intentionally.
    /// </summary>
    public bool TrustedBootstrapDone { get; set; } = false;
}
