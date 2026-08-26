<#
.SYNOPSIS
Re-syncs the docs/generic/src snapshot mirror from its live originals.

.DESCRIPTION
docs/generic/src/sync-manifest.txt is the single source of truth: each line maps a
snapshot path to the engine-side original it mirrors. Default mode copies every origin
over its snapshot. -Check copies nothing, lists drifted snapshots and exits 1.

The localization example pair (src/localization/ModStrings.cs, english.json) and
src/bootstrap/gitignore.example are deliberately not mirrors and are absent from the
manifest; the test suite guards the example pair's compile floor instead.
#>
[CmdletBinding()]
param([switch]$Check)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$manifest = Join-Path $root 'docs/generic/src/sync-manifest.txt'
if (-not (Test-Path $manifest)) { throw "Manifest not found: $manifest" }

$drifted = @()
$missing = @()

foreach ($line in (Get-Content $manifest)) {
    $text = $line.Trim()
    if ($text -eq '' -or $text.StartsWith('#')) { continue }
    $parts = $text.Split('|')
    if ($parts.Count -ne 2) { throw "Malformed manifest line: $text" }
    $snapshot = Join-Path $root $parts[0].Trim()
    $origin = Join-Path $root $parts[1].Trim()
    if (-not (Test-Path $origin)) { $missing += $parts[1].Trim(); continue }

    $same = $false
    if (Test-Path $snapshot) {
        $same = (Get-FileHash $snapshot -Algorithm SHA256).Hash -eq (Get-FileHash $origin -Algorithm SHA256).Hash
    }
    if ($same) { continue }

    $drifted += $parts[0].Trim()
    if (-not $Check) {
        $dir = Split-Path $snapshot -Parent
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        Copy-Item -LiteralPath $origin -Destination $snapshot -Force
    }
}

foreach ($m in $missing) { Write-Warning "Origin missing: $m" }

if ($Check) {
    if ($drifted.Count -eq 0 -and $missing.Count -eq 0) {
        Write-Host 'docs/generic/src is in sync with its originals.'
        exit 0
    }
    foreach ($d in $drifted) { Write-Host "DRIFT $d" }
    Write-Host "$($drifted.Count) snapshot(s) drifted. Run .\sync-generic-src.ps1 to refresh."
    exit 1
}

if ($drifted.Count -eq 0) {
    Write-Host 'docs/generic/src already in sync; nothing copied.'
} else {
    foreach ($d in $drifted) { Write-Host "synced $d" }
    Write-Host "$($drifted.Count) snapshot(s) refreshed."
}
if ($missing.Count -gt 0) { exit 1 }
