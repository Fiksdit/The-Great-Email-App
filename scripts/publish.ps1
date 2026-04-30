# FILE: scripts/publish.ps1
# Builds a release zip ready to upload as a GitHub Release asset.
#
# Usage (from repo root):
#   pwsh scripts/publish.ps1
#   pwsh scripts/publish.ps1 -Version 0.3.0     # bumps csproj <Version> first
#
# Output:
#   dist/GreatEmailApp-v<version>.zip
#
# Release flow:
#   1. Run this script.
#   2. Tag the commit:  git tag v<version> && git push --tags
#   3. On GitHub: Releases → Draft a new release → pick the tag, attach the zip,
#      paste release notes into the description.
#   4. Publish.
# Existing installs see it within seconds via the in-app About → Check for updates.

[CmdletBinding()]
param(
    [string]$Version,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$Root      = Split-Path -Parent $PSScriptRoot
$Csproj    = Join-Path $Root "src/GreatEmailApp/GreatEmailApp.csproj"
$DistDir   = Join-Path $Root "dist"
$PublishDir = Join-Path $Root "src/GreatEmailApp/bin/$Configuration/net8.0-windows/win-x64/publish"

if ($Version) {
    Write-Host "→ Bumping version to $Version in $Csproj" -ForegroundColor Cyan
    $xml = [xml](Get-Content $Csproj)
    $pg  = $xml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1
    $pg.Version         = $Version
    $pg.AssemblyVersion = "$Version.0"
    $pg.FileVersion     = "$Version.0"
    $xml.Save($Csproj)
}

# Read whatever version is in the csproj now.
$xml = [xml](Get-Content $Csproj)
$pg  = $xml.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1
$Resolved = $pg.Version
if (-not $Resolved) { throw "Could not read <Version> from $Csproj" }
Write-Host "→ Publishing GreatEmailApp v$Resolved ($Configuration)" -ForegroundColor Cyan

# Clean publish dir to make sure stale files don't ride along.
if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }

# net8.0-windows WPF — framework-dependent, win-x64. Smaller download than
# self-contained, and the target machines already have the .NET 8 runtime.
& dotnet publish $Csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained false `
    -p:PublishReadyToRun=false `
    -p:PublishSingleFile=false `
    -p:Version=$Resolved
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }

if (-not (Test-Path $DistDir)) { New-Item -ItemType Directory -Path $DistDir | Out-Null }

$ZipName = "GreatEmailApp-v$Resolved.zip"
$ZipPath = Join-Path $DistDir $ZipName
if (Test-Path $ZipPath) { Remove-Item $ZipPath }

Write-Host "→ Zipping $PublishDir → $ZipPath" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $PublishDir "*") -DestinationPath $ZipPath -Force

$bytes = (Get-Item $ZipPath).Length
$mb    = [math]::Round($bytes / 1MB, 2)
Write-Host ""
Write-Host "✓ Built  $ZipName  ($mb MB)" -ForegroundColor Green
Write-Host ""
Write-Host "Next:" -ForegroundColor Yellow
Write-Host "  git add -A && git commit -m 'Release v$Resolved' && git push" -ForegroundColor Gray
Write-Host "  git tag v$Resolved && git push --tags" -ForegroundColor Gray
Write-Host "  Open GitHub → Releases → Draft new release → tag v$Resolved → upload $ZipName" -ForegroundColor Gray
