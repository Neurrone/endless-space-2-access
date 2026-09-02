<#
.SYNOPSIS
Turns a translator's finished batch parts into the files the mod ships, and refuses to write
anything until they answer for every key the mod speaks.

.DESCRIPTION
The parts come back one batch at a time, from different sittings and possibly different
translators, so the failures worth catching are the ones no single batch can see: a key nobody
took, a key taken twice with two different answers, a paucal form never written, a "{0}" that
fell out of a sentence, a movie that came back with a cue too few. All of those are checked
across the whole set BEFORE anything is written, and the report names each one, so a run either
writes complete files or writes nothing.

The shipped files are written in english.json's own order - a paucal form directly after the
pair it belongs to - as UTF-8 without a byte order mark, two-space indented, with the
language's own letters written as themselves. Then mark-translated.ps1 records the English each
string was made from, which is what tells a later run of the tests that a phrase was rewritten
out from under its translation.

    .\tools\locale\merge-parts.ps1 -Language polish -PartsDir D:\work\done -Check
    .\tools\locale\merge-parts.ps1 -Language polish -PartsDir D:\work\done

.PARAMETER Language
The game's own language name, which is also the file name: polish, french, schinese...

.PARAMETER PartsDir
The folder holding the <language> folder of translated parts - strings-*.json and, if they
were translated, descriptions-*.json.

.PARAMETER Check
Report and write nothing, not even the snapshots.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Language,
    [Parameter(Mandatory = $true)][string]$PartsDir,
    [switch]$Check
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'translation-lib.ps1')

if ($Language -eq 'english') {
    throw 'english.json IS the English; nothing merges into it.'
}

if (-not (Test-GameLanguage $Language)) {
    throw "$Language is not a language Endless Space 2 ships in: $((Get-GameLanguages) -join ', ')"
}

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$parts = Join-Path $PartsDir $Language
if (-not (Test-Path -LiteralPath $parts)) { throw "Not found: $parts" }

$english = Read-JsonOrdered (Join-Path $root 'ES2Access\locale\english.json')
$englishCues = Read-JsonOrdered (Join-Path $root 'ES2Access\descriptions\english.json')

$entries = Get-ModStringsEntries $root $english
$fieldToKey = @{}
foreach ($entry in $entries) {
    if (-not $entry.IsPrefix -and $entry.Field -ne '') { $fieldToKey[$entry.Field] = $entry.Key }
}
$manyKeys = Get-PluralManyKeys $root $fieldToKey
$paucal = Test-ThreeForm $Language
$few = Get-FewSuffix

$problems = New-Object System.Collections.ArrayList
function Add-Problem([string]$text) { [void]$problems.Add($text) }

# Batch order matters only for the report; a part numbered 10 sorts after one numbered 9.
function Get-Parts([string]$prefix) {
    return @(Get-ChildItem -LiteralPath $parts -Filter "$prefix-*.json" |
        Sort-Object -Property @{ Expression = {
            if ($_.BaseName -match '-(\d+)$') { [int]$Matches[1] } else { [int]::MaxValue }
        } }, Name)
}

# --- the strings --------------------------------------------------------------------------------

$stringParts = Get-Parts 'strings'
$translated = New-Object System.Collections.Specialized.OrderedDictionary
$cameFrom = @{}
foreach ($part in $stringParts) {
    $table = ConvertFrom-Json ([System.IO.File]::ReadAllText($part.FullName))
    foreach ($property in $table.PSObject.Properties) {
        $key = $property.Name
        $value = $property.Value
        if ($value -isnot [string]) {
            Add-Problem "$($part.Name): '$key' holds an object, not a translation - this looks like a batch INPUT file"
            continue
        }

        if ($translated.Contains($key)) {
            if ($translated[$key] -ne $value) {
                Add-Problem "'$key' is translated twice and differently, in $($cameFrom[$key]) and $($part.Name)"
            }

            continue
        }

        $translated[$key] = $value
        $cameFrom[$key] = $part.Name
    }
}

if ($stringParts.Count -eq 0) {
    Add-Problem "no strings-*.json parts under $parts"
}

foreach ($key in (Get-Names $english)) {
    if (-not $translated.Contains($key)) {
        Add-Problem "missing key '$key'"
        continue
    }

    if ([string]$translated[$key] -eq '') { Add-Problem "'$key' is empty" }
    if ($paucal -and $manyKeys.Contains($key) -and -not $translated.Contains($key + $few)) {
        Add-Problem "missing paucal form '$key$few'"
    }
}

foreach ($key in @($translated.Keys)) {
    $base = Get-BaseKey $key
    $isPaucal = Test-Paucal $key
    if (-not (Test-HasName $english $base)) {
        Add-Problem "unknown key '$key'"
        continue
    }

    if ($isPaucal) {
        if (-not $paucal) {
            Add-Problem "unknown key '$key': $Language has no paucal form"
            continue
        }

        if (-not $manyKeys.Contains($base)) {
            Add-Problem "unknown key '$key': '$base' is not the MANY key of a counted phrase"
            continue
        }
    }

    if ([string]$translated[$key] -eq '') {
        if ($isPaucal) { Add-Problem "'$key' is empty" }
        continue
    }

    $wanted = Get-Placeholders ([string]$english.$base)
    $got = Get-Placeholders ([string]$translated[$key])
    if ($wanted -ne $got) {
        $wantedText = if ($wanted -eq '') { 'none' } else { $wanted }
        $gotText = if ($got -eq '') { 'none' } else { $got }
        Add-Problem "'$key' has placeholders $gotText, English has $wantedText"
    }
}

# --- the cutscene descriptions ---------------------------------------------------------------------

$cueParts = Get-Parts 'descriptions'
$cues = New-Object System.Collections.Specialized.OrderedDictionary
$cueCameFrom = @{}
foreach ($part in $cueParts) {
    $table = ConvertFrom-Json ([System.IO.File]::ReadAllText($part.FullName))
    foreach ($property in $table.PSObject.Properties) {
        $movie = $property.Name
        $rows = @($property.Value)
        if ($rows.Count -gt 0 -and $rows[0] -isnot [string]) {
            Add-Problem "$($part.Name): '$movie' holds cue objects, not translations - this looks like a batch INPUT file"
            continue
        }

        if ($cues.Contains($movie)) {
            Add-Problem "'$movie' is described twice, in $($cueCameFrom[$movie]) and $($part.Name)"
            continue
        }

        $cues[$movie] = $rows
        $cueCameFrom[$movie] = $part.Name
    }
}

if ($cueParts.Count -gt 0) {
    foreach ($movie in (Get-Names $englishCues)) {
        if (-not $cues.Contains($movie)) {
            Add-Problem "'$movie' is described in English and not here"
            continue
        }

        $source = @($englishCues.$movie)
        $rows = @($cues[$movie])
        if ($rows.Count -ne $source.Count) {
            Add-Problem "'$movie' has $($rows.Count) cues, English has $($source.Count)"
            continue
        }

        for ($i = 0; $i -lt $rows.Count; $i++) {
            if ([string]$rows[$i] -eq '') { Add-Problem "'$movie' cue $i is empty" }
        }
    }

    foreach ($movie in @($cues.Keys)) {
        if (-not (Test-HasName $englishCues $movie)) {
            Add-Problem "'$movie' is not a video English describes"
        }
    }
}

# --- the verdict ----------------------------------------------------------------------------------

Write-Host "$($stringParts.Count) strings part(s), $($cueParts.Count) descriptions part(s) under $parts"
if ($problems.Count -gt 0) {
    # Truncated the way the C# lints truncate: a wholly wrong set of parts should report a
    # diagnosis rather than a wall.
    $shown = [Math]::Min($problems.Count, 40)
    Write-Host "$($problems.Count) problem(s):"
    foreach ($problem in $problems[0..($shown - 1)]) { Write-Host "  $problem" }
    if ($problems.Count -gt $shown) { Write-Host "  ... and $($problems.Count - $shown) more" }
    throw "$Language is not complete; nothing written."
}

if ($Check) {
    Write-Host "$($translated.Count) string(s) and $($cues.Count) movie(s) check out. -Check, so nothing written."
    return
}

# --- writing --------------------------------------------------------------------------------------

$localePath = Join-Path $root "ES2Access\locale\$Language.json"
$lines = @()
foreach ($key in (Get-Names $english)) {
    $lines += '  ' + (Format-JsonString $key) + ': ' + (Format-JsonString ([string]$translated[$key]))
    $paucalKey = $key + $few
    if ($translated.Contains($paucalKey)) {
        $lines += '  ' + (Format-JsonString $paucalKey) + ': ' + (Format-JsonString ([string]$translated[$paucalKey]))
    }
}

Write-Utf8 $localePath ("{`n" + ($lines -join ",`n") + "`n}`n")
Write-Host "$($localePath.Substring($root.Length).TrimStart('\')) : $($lines.Count) entr(ies)"

if ($cueParts.Count -gt 0) {
    $descriptionsPath = Join-Path $root "ES2Access\descriptions\$Language.json"
    $lines = @()
    foreach ($movie in (Get-Names $englishCues)) {
        $source = @($englishCues.$movie)
        $rows = @($cues[$movie])
        $items = @()
        for ($i = 0; $i -lt $source.Count; $i++) {
            # The timings belong to the footage, so they come from English rather than from a part.
            $items += '    { "at": ' + (Format-JsonNumber $source[$i].at) +
                ', "end": ' + (Format-JsonNumber $source[$i].end) +
                ', "text": ' + (Format-JsonString ([string]$rows[$i])) + ' }'
        }

        $lines += '  ' + (Format-JsonString $movie) + ': [' + "`n" + ($items -join ",`n") + "`n  ]"
    }

    Write-Utf8 $descriptionsPath ("{`n" + ($lines -join ",`n") + "`n}`n")
    Write-Host "$($descriptionsPath.Substring($root.Length).TrimStart('\')) : $($cues.Count) movie(s)"
}

# The snapshots are the record of WHICH English each string was translated from, and these
# strings were just made from the English on disk, so the whole file is recorded.
& (Join-Path $PSScriptRoot 'mark-translated.ps1') -Language $Language
if ($cueParts.Count -gt 0) {
    & (Join-Path $PSScriptRoot 'mark-translated.ps1') -Language $Language -Descriptions
} else {
    Write-Warning "No descriptions parts; ES2Access\descriptions\$Language.json left as it was."
}
