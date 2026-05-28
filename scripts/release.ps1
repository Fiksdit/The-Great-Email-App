<#
.SYNOPSIS
  Sign + verify GreatEmailApp.exe via Microsoft Trusted Signing.

.DESCRIPTION
  Bolts onto publish.ps1's output. Takes the published
  `GreatEmailApp.exe` (the main exe inside the publish dir, NOT the
  zip) and:

    1. Ensures the Trusted Signing CSP/dlib (`Azure.CodeSigning.Dlib`)
       is installed locally
    2. Authenticates with Azure (`az login` interactive by default;
       service-principal env vars also supported for CI)
    3. Calls `signtool sign` against the exe using the Trusted Signing
       dlib
    4. Verifies the resulting signature is valid + timestamped
    5. Prints SHA-256 of the signed artifact for release notes

  Closes P2-TD-7 "Code signing for the installer" from the roadmap.
  Ported from TGFF's release.ps1 (2026-05-21) because the same Trusted
  Signing account + cert profile cover both apps -- see
  "Why the cert profile is named PrintMaestro" in scripts/SIGNING.md.

  Recommended flow:
    1. Run publish.ps1 to produce the publish dir
    2. Run THIS script to sign GreatEmailApp.exe in place
    3. Then have publish.ps1 (or you, manually) zip the now-signed
       publish dir into dist/GreatEmailApp-v<ver>.zip

  Zips themselves can't be signed in any meaningful way -- the signed
  artifact is the exe inside.

.PARAMETER ArtifactPath
  Path to GreatEmailApp.exe to sign. Default:
    ..\src\GreatEmailApp\bin\Release\net8.0-windows\win-x64\publish\GreatEmailApp.exe
  Override when signing a Debug build or non-default publish dir.

.PARAMETER EndpointSlug
  Trusted Signing account "endpoint". Resolution order:
  -EndpointSlug arg > $env:GREATEMAIL_TS_ENDPOINT > project default
  (`cus.codesigning.azure.net`).

.PARAMETER AccountName
  Trusted Signing account name. Resolution order:
  -AccountName arg > $env:GREATEMAIL_TS_ACCOUNT > project default
  (`Fiksdit-Signing`).

.PARAMETER CertificateProfile
  Cert profile under the account. Trusted Signing Basic tier caps at
  one Public Trust cert profile per account, so the Email Bin reuses
  the shared `PrintMaestro` profile (CN=FIKS'D IT). Resolution order:
  -CertificateProfile arg > $env:GREATEMAIL_TS_PROFILE > project default
  (`PrintMaestro`).

.PARAMETER SignToolPath
  Override location of signtool.exe.

.PARAMETER SkipAzLogin
  Don't run `az login` -- assumes the session is already authenticated.

.NOTES
  Requires:
    - Azure CLI (winget Microsoft.AzureCLI)
    - Windows SDK signtool.exe (winget Microsoft.WindowsSDK.10.0.22621)
    - Microsoft Trusted Signing account configured at Azure portal
    - Publisher verification completed (one-time, ~3 days)

  See scripts/SIGNING.md for the full account / cert-profile values
  and the rationale for sharing PrintMaestro across all 5DLS apps.

.EXAMPLE
  # Sign after publish.ps1 has produced the publish dir.
  PS> .\scripts\publish.ps1
  PS> .\scripts\release.ps1
  PS> # Then re-run publish.ps1 to re-zip the now-signed exe.

.EXAMPLE
  # Sign an arbitrary build.
  PS> .\scripts\release.ps1 -ArtifactPath C:\some\other\publish\GreatEmailApp.exe
#>

[CmdletBinding()]
param(
  [string]$ArtifactPath,
  [string]$EndpointSlug    = $env:GREATEMAIL_TS_ENDPOINT,
  [string]$AccountName     = $env:GREATEMAIL_TS_ACCOUNT,
  [string]$CertificateProfile = $env:GREATEMAIL_TS_PROFILE,
  [string]$SignToolPath,
  [switch]$SkipAzLogin
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Default artifact = the published exe in src\GreatEmailApp\bin\Release\.
# publish.ps1 puts it there with the same path shape; we just resolve
# relative to the repo root.
if (-not $ArtifactPath) {
  $repoRoot = Split-Path -Parent (Split-Path -Parent $PSCommandPath)
  $ArtifactPath = Join-Path $repoRoot 'src\GreatEmailApp\bin\Release\net8.0-windows\win-x64\publish\GreatEmailApp.exe'
}

# Trusted Signing project defaults: discovered from the Fiksdit-Signing
# account (Central US, Basic tier). See scripts/SIGNING.md for the
# full setup notes + Azure portal links. Override via -arg or env var.
# Basic tier limits the account to ONE Public Trust certificate
# profile, so the Email Bin reuses the existing PrintMaestro slot;
# the cert subject (CN=FIKS'D IT) is identical regardless of the
# Azure label.
if (-not $EndpointSlug)        { $EndpointSlug        = 'cus.codesigning.azure.net' }
if (-not $AccountName)         { $AccountName         = 'Fiksdit-Signing' }
if (-not $CertificateProfile)  { $CertificateProfile  = 'PrintMaestro' }

function Write-Step { param($m) Write-Host "`n>>> $m" -ForegroundColor Cyan }
function Write-Ok   { param($m) Write-Host "    [ OK ] $m" -ForegroundColor Green }
function Write-Note { param($m) Write-Host "    $m"        -ForegroundColor DarkGray }
function Write-Fail { param($m) Write-Host "    [FAIL] $m" -ForegroundColor Red }

$ScriptDir = Split-Path -Parent $PSCommandPath
$DlibDir   = Join-Path $ScriptDir 'trusted-signing-dlib'

function Resolve-Signtool {
  if ($SignToolPath -and (Test-Path $SignToolPath)) { return $SignToolPath }
  $sdkRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
  if (Test-Path $sdkRoot) {
    $versions = Get-ChildItem $sdkRoot -Directory |
      Where-Object { $_.Name -match '^10\.' } |
      Sort-Object Name -Descending
    foreach ($v in $versions) {
      $candidate = Join-Path $v.FullName 'x64\signtool.exe'
      if (Test-Path $candidate) { return $candidate }
    }
  }
  $onPath = Get-Command signtool -ErrorAction SilentlyContinue
  if ($onPath) { return $onPath.Source }
  return $null
}

function Resolve-Az {
  $az = Get-Command az -ErrorAction SilentlyContinue
  if ($az) { return $az.Source }
  return $null
}

function Ensure-TrustedSigningDlib {
  $dll = Join-Path $DlibDir 'bin\x64\Azure.CodeSigning.Dlib.dll'
  if (Test-Path $dll) { return $dll }

  Write-Note 'Resolving Microsoft.Trusted.Signing.Client latest version...'
  $indexUrl = 'https://api.nuget.org/v3-flatcontainer/microsoft.trusted.signing.client/index.json'
  $index = Invoke-RestMethod -Uri $indexUrl -UseBasicParsing -TimeoutSec 30
  $tsClientVersion = $index.versions[-1]
  Write-Note "  latest = $tsClientVersion"

  $nupkgUrl = "https://api.nuget.org/v3-flatcontainer/microsoft.trusted.signing.client/$tsClientVersion/microsoft.trusted.signing.client.$tsClientVersion.nupkg"
  $tmp = Join-Path $env:TEMP "trusted-signing-client-$tsClientVersion.nupkg"
  Invoke-WebRequest -Uri $nupkgUrl -OutFile $tmp -UseBasicParsing
  if (-not (Test-Path $tmp)) { throw 'Trusted Signing Client download failed' }

  if (Test-Path $DlibDir) { Remove-Item -Recurse -Force $DlibDir }
  New-Item -ItemType Directory -Force -Path $DlibDir | Out-Null
  $tmpZip = "$tmp.zip"
  Copy-Item $tmp $tmpZip -Force
  Expand-Archive -Path $tmpZip -DestinationPath $DlibDir -Force
  Remove-Item $tmp, $tmpZip -ErrorAction SilentlyContinue

  if (-not (Test-Path $dll)) { throw "Dlib extract incomplete: $dll missing" }
  return $dll
}

# --- preflight -------------------------------------------------------------

Write-Host ''
Write-Host 'GreatEmailApp release signer (Trusted Signing)' -ForegroundColor White
Write-Host "Artifact: $ArtifactPath" -ForegroundColor DarkGray

if (-not (Test-Path $ArtifactPath)) {
  Write-Fail "ArtifactPath not found: $ArtifactPath"
  Write-Note 'Run scripts\publish.ps1 first to produce the publish dir.'
  exit 1
}
$ext = [System.IO.Path]::GetExtension($ArtifactPath).ToLowerInvariant()
if ($ext -notin @('.exe', '.dll', '.ps1', '.msi', '.cab')) {
  Write-Fail "signtool refuses to sign $ext files"
  exit 1
}

foreach ($pair in @(
  @{name='EndpointSlug';value=$EndpointSlug;env='GREATEMAIL_TS_ENDPOINT'},
  @{name='AccountName';value=$AccountName;env='GREATEMAIL_TS_ACCOUNT'},
  @{name='CertificateProfile';value=$CertificateProfile;env='GREATEMAIL_TS_PROFILE'}
)) {
  if (-not $pair.value) {
    Write-Fail "$($pair.name) not set. Pass -$($pair.name) or set $($pair.env)."
    exit 1
  }
}

# --- 1. signtool -----------------------------------------------------------

Write-Step 'Step 1/4: locate signtool.exe'
$Signtool = Resolve-Signtool
if (-not $Signtool) {
  Write-Fail 'signtool.exe not found. Install the Windows SDK (winget Microsoft.WindowsSDK.10.0.22621).'
  exit 1
}
Write-Ok "signtool: $Signtool"

# --- 2. Azure auth --------------------------------------------------------

Write-Step 'Step 2/4: Azure authentication'
$Az = Resolve-Az
if (-not $Az) {
  Write-Fail 'az.exe not found. Install the Azure CLI (winget Microsoft.AzureCLI).'
  exit 1
}
Write-Note "az: $Az"

if ($SkipAzLogin) {
  Write-Note 'SkipAzLogin set; assuming current session is authenticated.'
} else {
  $whoami = & $Az account show --only-show-errors 2>$null
  if ($LASTEXITCODE -eq 0) {
    Write-Note 'az session already authenticated.'
  } else {
    Write-Note 'az login (browser will open)...'
    & $Az login --only-show-errors | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'az login failed' }
  }
}
Write-Ok 'authenticated'

# --- 3. Trusted Signing dlib ----------------------------------------------

Write-Step 'Step 3/4: ensure Trusted Signing dlib is present'
$Dlib = Ensure-TrustedSigningDlib
Write-Ok "dlib: $Dlib"

$metadata = @{
  Endpoint = "https://$EndpointSlug"
  CodeSigningAccountName = $AccountName
  CertificateProfileName = $CertificateProfile
  CorrelationId = [Guid]::NewGuid().ToString()
} | ConvertTo-Json -Depth 3
$metadataPath = Join-Path $env:TEMP "greatemail-ts-metadata-$([Guid]::NewGuid()).json"
$metadata | Set-Content -Path $metadataPath -Encoding UTF8

# --- 4. sign + verify ------------------------------------------------------

try {
  Write-Step 'Step 4/4: sign + verify'
  Write-Note "signing $ArtifactPath"
  & $Signtool sign `
    /v `
    /debug `
    /fd SHA256 `
    /td SHA256 `
    /tr 'http://timestamp.acs.microsoft.com' `
    /dlib $Dlib `
    /dmdf $metadataPath `
    $ArtifactPath
  if ($LASTEXITCODE -ne 0) { throw "signtool sign failed (exit $LASTEXITCODE)" }
  Write-Ok 'signed'

  Write-Note 'verifying signature + timestamp'
  & $Signtool verify /v /pa $ArtifactPath
  if ($LASTEXITCODE -ne 0) { throw "signtool verify failed (exit $LASTEXITCODE)" }
  Write-Ok 'verified'

  $signedSize = (Get-Item $ArtifactPath).Length
  $signedHash = (Get-FileHash -Algorithm SHA256 $ArtifactPath).Hash

  Write-Host ''
  Write-Host 'Release artifact ready.' -ForegroundColor Green
  Write-Host "  File:    $ArtifactPath"
  Write-Host ("  Size:    {0:N0} bytes ({1:N1} MiB)" -f $signedSize, ($signedSize/1MB))
  Write-Host "  SHA-256: $signedHash"
  Write-Host ''
  Write-Host 'Next: re-run scripts\publish.ps1 (without -Version) to re-zip'
  Write-Host '      the now-signed publish dir into dist\GreatEmailApp-v<ver>.zip.'
  Write-Host ''
  Write-Host 'Heads up: SmartScreen reputation is build-up, not EV-instant.'
  Write-Host 'First few hundred downloads of any new artifact still show'
  Write-Host '"Windows protected your PC" until enough installs accrue.'
} finally {
  Remove-Item $metadataPath -ErrorAction SilentlyContinue
}
