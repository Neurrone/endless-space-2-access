<#
.SYNOPSIS
Records the English a translation was made from, so the tests can tell a stale one from a
current one.

.DESCRIPTION
Nothing at runtime can notice that an English phrase was rewritten out from under a
translation: both files still hold a sentence, and the mod speaks the translated one. The
snapshots under ES2Access\locale\sources and ES2Access\descriptions\sources are the only
record of which English text each translated string was actually written against, and
TranslationLintTests / DescriptionFileTests fail on any key whose recorded English no longer
matches english.json.

So the contract is: whoever translates or re-checks a key runs this afterwards, naming the
keys they looked at. Running it with no -Keys marks the WHOLE file translated, which is right
for a fresh translation and wrong for a touch-up - it would silently bless every phrase whose
English moved while nobody was looking.

    # a whole new translation, or a full re-check
    .\tools\locale\mark-translated.ps1 -Language polish

    # after re-checking three phrases against their new English
    .\tools\locale\mark-translated.ps1 -Language polish -Keys galaxy.fleet.moving,galaxy.fleet.idle

    # the cutscene audio descriptions instead of the string table
    .\tools\locale\mark-translated.ps1 -Language polish -Descriptions

The snapshot is keyed exactly like the translation it sits beside, the extra counted forms
(.few, .one) included; each of those records the English of the pair's MANY sentence, which is
what it was written from. Output is UTF-8 without a byte order mark, two-space indented, in
english.json's own key order, which is what the encoding lint expects.

sources\ is a subfolder because both the build's copy step and build_release.ps1 take
locale\*.json without recursing: the snapshots never reach a player's install.

.PARAMETER Language
The game's own language name, which is also the file name: polish, russian, schinese...

.PARAMETER Keys
Only these keys (or, with -Descriptions, only these videos). Everything already recorded is
left as it was.

.PARAMETER Descriptions
Work on descriptions\ rather than locale\.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Language,
    [string[]]$Keys,
    [switch]$Descriptions
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

if ($Language -eq 'english') {
    throw 'english.json IS the English; there is nothing for it to be translated from.'
}

$folder = if ($Descriptions) { 'descriptions' } else { 'locale' }
$dir = Join-Path $root "ES2Access\$folder"
$translation = Join-Path $dir "$Language.json"
$english = Join-Path $dir 'english.json'
$sourcesDir = Join-Path $dir 'sources'
$snapshotPath = Join-Path $sourcesDir "$Language.json"

foreach ($required in @($translation, $english)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Not found: $required" }
}

# Property order survives ConvertFrom-Json, and english.json's order is the order a translator
# reads the file in, so the snapshot is written in it too.
function Read-Ordered([string]$path) {
    # No explicit encoding: ReadAllText detects and strips a byte order mark, and a file that
    # still has one is the encoding lint's business, not this script's.
    return ConvertFrom-Json ([System.IO.File]::ReadAllText($path))
}

function Names($object) {
    return @($object.PSObject.Properties | ForEach-Object { $_.Name })
}

function Has($object, [string]$name) {
    return $null -ne $object.PSObject.Properties[$name]
}

# Hand-written rather than ConvertTo-Json: Windows PowerShell escapes non-ASCII to \uXXXX and
# indents four spaces, and the snapshots are read back by a lint that checks the bytes.
function Escape([string]$text) {
    $out = New-Object System.Text.StringBuilder
    foreach ($c in $text.ToCharArray()) {
        switch ([string]$c) {
            '"' { [void]$out.Append('\"'); continue }
            '\' { [void]$out.Append('\\'); continue }
            "`b" { [void]$out.Append('\b'); continue }
            "`f" { [void]$out.Append('\f'); continue }
            "`n" { [void]$out.Append('\n'); continue }
            "`r" { [void]$out.Append('\r'); continue }
            "`t" { [void]$out.Append('\t'); continue }
            default {
                if ([int]$c -lt 0x20) {
                    [void]$out.AppendFormat('\u{0:x4}', [int]$c)
                } else {
                    [void]$out.Append($c)
                }
            }
        }
    }
    return $out.ToString()
}

function Write-Json([string]$path, [string[]]$lines) {
    $body = "{`n" + ($lines -join ",`n") + "`n}`n"
    if (-not (Test-Path -LiteralPath $sourcesDir)) {
        New-Item -ItemType Directory -Path $sourcesDir -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($path, $body, [System.Text.UTF8Encoding]::new($false))
}

$englishTable = Read-Ordered $english
$translationTable = Read-Ordered $translation
$snapshot = if (Test-Path -LiteralPath $snapshotPath) { Read-Ordered $snapshotPath } else { $null }
$wanted = if ($Keys) { [System.Collections.Generic.HashSet[string]]::new([string[]]$Keys) } else { $null }

$lines = @()
$marked = 0
$kept = 0

if ($Descriptions) {
    foreach ($movie in Names $englishTable) {
        if (-not (Has $translationTable $movie)) { continue }
        if ($wanted -and -not $wanted.Contains($movie)) {
            if ($snapshot -and (Has $snapshot $movie)) {
                $texts = @($snapshot.$movie)
                $kept++
            } else {
                continue
            }
        } else {
            $texts = @($englishTable.$movie | ForEach-Object { $_.text })
            $marked++
        }

        $items = @($texts | ForEach-Object { '    "' + (Escape $_) + '"' })
        $lines += '  "' + (Escape $movie) + '": [' + "`n" + ($items -join ",`n") + "`n  ]"
    }
} else {
    foreach ($key in Names $englishTable) {
        # The extra counted forms sit beside their pair, and record the same English sentence.
        foreach ($name in @($key, "$key.few", "$key.one")) {
            if (-not (Has $translationTable $name)) { continue }
            if ($wanted -and -not $wanted.Contains($name) -and -not $wanted.Contains($key)) {
                if ($snapshot -and (Has $snapshot $name)) {
                    $value = $snapshot.$name
                    $kept++
                } else {
                    continue
                }
            } else {
                $value = $englishTable.$key
                $marked++
            }

            $lines += '  "' + (Escape $name) + '": "' + (Escape $value) + '"'
        }
    }
}

if ($lines.Count -eq 0) {
    throw "Nothing to record: $translation answers none of english.json's keys."
}

Write-Json $snapshotPath $lines

$relative = $snapshotPath.Substring($root.Length).TrimStart('\')
Write-Host "$relative : $marked entr(ies) marked translated, $kept left as recorded."
if ($wanted) {
    $unseen = @($Keys | Where-Object { -not (Has $translationTable $_) })
    foreach ($name in $unseen) {
        Write-Warning "not in $Language.json, nothing recorded: $name"
    }
}
