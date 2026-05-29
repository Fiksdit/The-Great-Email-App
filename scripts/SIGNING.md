# GreatEmailApp release signing

Code-signing pipeline for `GreatEmailApp.exe` produced by `publish.ps1`.
Closes the **P2-TD-7 "Code signing for the installer"** roadmap item.

The setup is identical to TGFF/TGFR/TGFO by design — the same Azure
Trusted Signing account, certificate profile, and CN are reused
across all 5DLS apps (PrintMaestro, TGFF, TGFO, TGFR, Email Bin).
See [Why the cert profile is named PrintMaestro](#why-the-cert-profile-is-named-printmaestro).

## One-time tooling install

```powershell
winget install Microsoft.AzureCLI                 # ~500 MB
winget install Microsoft.WindowsSDK.10.0.22621    # ~6 GB
```

These install once per machine and are then shared across every 5DLS
app that needs to sign. If you've already done this for any other
5DLS app, you're done — `release.ps1` finds them automatically.

> **✅ Tooling status on James's main desktop (`B:`) — installed 2026-05-28:**
> - Azure CLI 2.86.0 at `C:\Program Files\Microsoft SDKs\Azure\CLI2\`
> - Windows SDK 10.0.26100.0 at `C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe`
> - `Artifact Signing Certificate Profile Signer` role granted to
>   `jreed_fiksdit.com#EXT#@jreedfiksdit.onmicrosoft.com` on the
>   `PrintMaestro` cert profile.
>
> No further per-machine or per-account setup needed on this box.
> Same applies to every other 5DLS repo — they all use the same shared
> tooling. First-ever successful sign was TGFO.App.exe on 2026-05-28
> 18:12 — see TGFO commit `e0ee7bb` for the SHA-256 + signed-at timestamp.

## First-time per-Azure-account setup (one-time per signing identity)

The first user to sign with a given Azure account against the
`PrintMaestro` cert profile needs to grant themselves the **Artifact
Signing Certificate Profile Signer** role on that profile. Generic
Subscription Owner doesn't cover it — the Trusted Signing API checks
for that specific role and fails with `SignerSign() failed` /
`0x80004005` if it's missing.

Run once per Azure user account, after `az login` succeeds:

```powershell
$me = az ad signed-in-user show --query id -o tsv
$profileScope = "/subscriptions/7376ef6f-9922-4235-a540-89bcaf58618f/resourceGroups/trusted-signing/providers/Microsoft.CodeSigning/codeSigningAccounts/Fiksdit-Signing/certificateProfiles/PrintMaestro"
az role assignment create `
  --role "Artifact Signing Certificate Profile Signer" `
  --assignee $me `
  --scope $profileScope
```

Wait ~60s for the role to propagate, then run `release.ps1`.

> **Heads up about the role name:** the role used to be called
> `Trusted Signing Certificate Profile Signer`. Microsoft renamed
> the entire product Trusted Signing → Artifact Signing at some point;
> documentation predating ~mid-2026 still uses the old name and
> **will fail** with `Role 'Trusted Signing Certificate Profile Signer'
> doesn't exist.`

## Build → sign → ship

The recommended flow:

```powershell
# 1. Build the publish dir (existing flow — produces the .exe inside,
#    no zip yet if you stop before the Compress-Archive step).
pwsh scripts/publish.ps1

# 2. Sign GreatEmailApp.exe in place inside the publish dir.
pwsh scripts/release.ps1

# 3. Re-run publish.ps1 to zip the now-signed publish dir.
#    (publish.ps1 cleans the publish dir on each run, so you need
#     to sign BEFORE the zip step. Today publish.ps1 doesn't have a
#     "-SkipBuild" switch — pending a small refactor that splits
#     build / zip into separate cmdlets so this can be one command.)
```

Zips can't be signed in any meaningful way — the signed artifact is
the `.exe` inside the zip. Windows / SmartScreen care about the
`.exe`, not the wrapper.

## What the script does

1. Locates `signtool.exe` under the Windows SDK.
2. Opens a browser for `az login` (one-time per shell session).
3. Downloads `Microsoft.Trusted.Signing.Client` from NuGet into
   `scripts/trusted-signing-dlib/` (cached after first run; about
   10 MB, gitignored).
4. Calls `signtool sign /dlib ... /dmdf <metadata>.json GreatEmailApp.exe`
   against the Trusted Signing endpoint.
5. Calls `signtool verify /v /pa GreatEmailApp.exe` to confirm.
6. Prints the final SHA-256 for release notes.

## Trusted Signing account values

| Param | Env var | Default |
|-------|---------|---------|
| `-EndpointSlug` | `GREATEMAIL_TS_ENDPOINT` | `cus.codesigning.azure.net` (Central US) |
| `-AccountName` | `GREATEMAIL_TS_ACCOUNT` | `Fiksdit-Signing` |
| `-CertificateProfile` | `GREATEMAIL_TS_PROFILE` | `PrintMaestro` |

Resolution order per value: `-arg` > env var > project default.

## Why the cert profile is named `PrintMaestro`

The `Fiksdit-Signing` account was originally set up for the
print-maestro project. **Trusted Signing Basic tier caps the account
at one Public Trust certificate profile per account**, so every 5DLS
app reuses the existing `PrintMaestro` slot rather than creating
separate profiles per app. The Azure resource name is just a label —
the cert that ships in every signed installer has:

```
Subject: CN=FIKS'D IT, O=FIKS'D IT, L=Sedalia, S=Missouri, C=US
```

That's the **Publisher** value Windows displays regardless of which
app is being signed. Upgrading to Premium tier would unlock a second
cert profile if per-app isolation becomes important later.

## Reference Azure values

| Value | What |
|-------|------|
| Subscription friendly name | `5DLS` |
| Subscription ID | `7376ef6f-9922-4235-a540-89bcaf58618f` |
| Resource group | `trusted-signing` |
| Tenant friendly name | `jreedfiksdit.onmicrosoft.com` (a.k.a. `Default Directory`) |
| **Tenant ID** | `a907e5a2-c0fe-4581-b73d-70c3fcbefd79` |
| Identity validation ID | `b410387b-0fc3-45c7-850b-7e4381f6b666` (CN: `FIKS'D IT`) |
| Account name (cert account) | `Fiksdit-Signing` |
| Cert profile name | `PrintMaestro` |
| Cert profile EKU | `1.3.6.1.5.5.7.3.3` (code signing) |
| Leaf cert rotation | Auto — Microsoft issues a fresh short-lived cert per ~3 days; the `signtool /td` timestamp pins each signature to a valid-at-the-time chain. |

Direct portal links:
- [Fiksdit-Signing account overview](https://portal.azure.com/#@/resource/subscriptions/7376ef6f-9922-4235-a540-89bcaf58618f/resourceGroups/trusted-signing/providers/Microsoft.CodeSigning/codeSigningAccounts/Fiksdit-Signing/overview)
- [Certificate profiles list](https://portal.azure.com/#@/resource/subscriptions/7376ef6f-9922-4235-a540-89bcaf58618f/resourceGroups/trusted-signing/providers/Microsoft.CodeSigning/codeSigningAccounts/Fiksdit-Signing/certificateProfiles)

## Code-signing reputation note

Microsoft Trusted Signing (Basic tier) issues OV-equivalent certs.
That means the first few hundred downloads of any new artifact will
trigger SmartScreen's "Windows protected your PC" warning even with a
valid signature. The warning goes away as the artifact's reputation
builds (Microsoft's opaque metric, typically days-to-weeks of normal
download volume). EV-tier certs skip the warning instantly but cost
significantly more and aren't planned for the MVP.
