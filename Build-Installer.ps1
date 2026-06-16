<#
.SYNOPSIS
    Publishes Keep Awake as a self-contained EXE and wraps it in an Inno Setup installer.

.PARAMETER Version
    Version number to stamp (e.g. 1.2.3). Defaults to 1.0.0.

.PARAMETER Run
    Launch the installer immediately after building.

.EXAMPLE
    .\Build-Installer.ps1
    .\Build-Installer.ps1 -Version 1.2.0 -Run
#>
[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [switch]$Run
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# ── Prerequisites ──────────────────────────────────────────────────────────────

$iscc = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (!$iscc) {
    $iscc = (Get-Command iscc -ErrorAction SilentlyContinue)?.Source
}

if (!$iscc) {
    Write-Error "Inno Setup 6 not found.`nDownload from https://jrsoftware.org/isinfo.php"
    exit 1
}
Write-Host "Using Inno Setup: $iscc"

# ── Publish EXE ────────────────────────────────────────────────────────────────

$publishDir = "$root\installer\publish"

Write-Host ""
Write-Host "Publishing $Version (self-contained, x64)..."
dotnet publish "$root\Keep Awake\Keep Awake.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:Version=$Version `
    -o $publishDir

if (!(Test-Path "$publishDir\Keep Awake.exe")) {
    Write-Error "EXE not found after publish — dotnet publish may have failed."
    exit 1
}

# ── Build installer ────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "Building installer..."
& $iscc /DAppVersion="$Version" "$root\installer\KeepAwake.iss"

$exePath = "$root\installer\output\KeepAwake-Setup-$Version.exe"
if (!(Test-Path $exePath)) {
    Write-Error "Installer not found at expected path: $exePath"
    exit 1
}

Write-Host ""
Write-Host "Done: installer\output\KeepAwake-Setup-$Version.exe"

if ($Run) {
    Write-Host "Launching installer..."
    Start-Process $exePath
}
