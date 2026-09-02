<#
.SYNOPSIS
Cuts the mod's English strings and cutscene descriptions into batch files a translator can
work through one at a time.

.DESCRIPTION
A translator - human or model - handed a bare key-to-English table mistranslates the short
strings, because the mod's phrases are short by design ("edited", "blank", "x {0}") and a word
out of context is a coin toss. So each batch entry carries three things beside the English:

  context  the prose above the constant in ModStrings*.cs, which is where the mod already
           says what the string is for and why it is worded as it is
  terms    how the GAME itself already translates the proper nouns the string names, from the
           glossary built out of the shipped language files - a translation that renames
           Dust or the Academy reads as a different game
  forms    present only on the MANY key of a counted pair, naming the extra number forms this
           language owes on top of the key itself: "few" for the Slavic paucal, and "one"
           where the language's singular also covers 21, 31 ... and the pair's singular
           sentence has no number in it to say so with

Batches are input only: they are never edited in place. A translator writes NEW files - see
the README.txt written beside them - and merge-parts.ps1 turns those into the shipped files.

    .\tools\locale\export-batches.ps1 -Language polish
    .\tools\locale\export-batches.ps1 -Language french -BatchSize 150 -OutDir D:\work\batches

.PARAMETER Language
The game's own language name, which is also the file name: polish, french, schinese...

.PARAMETER BatchSize
Strings per strings batch, and the cue budget of a descriptions batch. A movie is never split
across two batches, so a descriptions batch overshoots by up to one movie.

.PARAMETER OutDir
Where the <language> folder of batch files goes.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Language,
    [int]$BatchSize = 300,
    [string]$OutDir = 'C:\Users\Dickson\AppData\Local\Temp\claude\C--Users-Dickson-Desktop-projects-endless-space-2-access\edfca2a9-1ed6-4356-80db-863d45ea20c3\scratchpad\batches'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'translation-lib.ps1')

if ($Language -eq 'english') {
    throw 'english.json IS the English; there is nothing to hand a translator.'
}

if (-not (Test-GameLanguage $Language)) {
    throw "$Language is not a language Endless Space 2 ships in: $((Get-GameLanguages) -join ', ')"
}

if ($BatchSize -lt 1) { throw 'BatchSize must be at least 1.' }

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$english = Read-JsonOrdered (Join-Path $root 'ES2Access\locale\english.json')
$descriptions = Read-JsonOrdered (Join-Path $root 'ES2Access\descriptions\english.json')

$entries = Get-ModStringsEntries $root $english
$fieldToKey = @{}
$contextOf = @{}
$prefixes = New-Object System.Collections.ArrayList
foreach ($entry in $entries) {
    if ($entry.IsPrefix) {
        [void]$prefixes.Add($entry)
        continue
    }

    if ($entry.Field -ne '') { $fieldToKey[$entry.Field] = $entry.Key }
    if (-not $contextOf.ContainsKey($entry.Key) -or $contextOf[$entry.Key] -eq '') {
        $contextOf[$entry.Key] = $entry.Context
    }
}

# Longest stem first, so "galaxy.system." wins over "galaxy." where both exist.
$prefixes = @($prefixes | Sort-Object -Property @{ Expression = { $_.Key.Length }; Descending = $true })

$pairs = Get-PluralPairs $root $fieldToKey
$paucal = Test-ThreeForm $Language
$largerSingular = Test-LargerSingular $Language
$terms = Get-TermIndex $root $Language

# What each MANY key owes this language beyond its own sentence, worked out once.
$formsOf = @{}
foreach ($manyKey in $pairs.Keys) {
    $forms = New-Object System.Collections.ArrayList
    if ($paucal) { [void]$forms.Add('few') }
    if ($largerSingular -and (Test-SemanticPair ([string]$english.($pairs[$manyKey])) ([string]$english.$manyKey))) {
        [void]$forms.Add('one')
    }

    if ($forms.Count -gt 0) { $formsOf[$manyKey] = @($forms) }
}

$target = Join-Path $OutDir $Language
if (Test-Path -LiteralPath $target) {
    Get-ChildItem -LiteralPath $target -Filter 'strings-*.json' | Remove-Item -Force
    Get-ChildItem -LiteralPath $target -Filter 'descriptions-*.json' | Remove-Item -Force
} else {
    New-Item -ItemType Directory -Path $target -Force | Out-Null
}

# --- the strings ------------------------------------------------------------------------------

$written = @()
$batch = New-Object System.Collections.Specialized.OrderedDictionary
$number = 1
$noContext = 0

function Save-Batch($table, [string]$prefix, [int]$index) {
    $path = Join-Path $target "$prefix-$index.json"
    Write-Utf8 $path ((ConvertTo-JsonText $table) + "`n")
    return $path
}

foreach ($key in (Get-Names $english)) {
    $item = New-Object System.Collections.Specialized.OrderedDictionary
    $item['en'] = [string]$english.$key
    $context = ''
    if ($contextOf.ContainsKey($key)) { $context = $contextOf[$key] }
    if ($context -eq '') {
        foreach ($prefix in $prefixes) {
            if ($key.StartsWith($prefix.Key)) { $context = $prefix.Context; break }
        }
    }

    if ($context -eq '') { $noContext++ }
    $item['context'] = $context
    if ($formsOf.ContainsKey($key)) { $item['forms'] = $formsOf[$key] }
    $item['terms'] = Get-TermsFor $terms "locale:$key"
    $batch[$key] = $item

    if ($batch.Count -ge $BatchSize) {
        $written += Save-Batch $batch 'strings' $number
        $number++
        $batch = New-Object System.Collections.Specialized.OrderedDictionary
    }
}

if ($batch.Count -gt 0) { $written += Save-Batch $batch 'strings' $number }

# --- the cutscene descriptions ------------------------------------------------------------------

$batch = New-Object System.Collections.Specialized.OrderedDictionary
$number = 1
$cues = 0
foreach ($movie in (Get-Names $descriptions)) {
    $rows = @($descriptions.$movie)
    if ($batch.Count -gt 0 -and ($cues + $rows.Count) -gt $BatchSize) {
        $written += Save-Batch $batch 'descriptions' $number
        $number++
        $batch = New-Object System.Collections.Specialized.OrderedDictionary
        $cues = 0
    }

    $list = New-Object System.Collections.ArrayList
    for ($i = 0; $i -lt $rows.Count; $i++) {
        $cue = New-Object System.Collections.Specialized.OrderedDictionary
        $cue['en'] = [string]$rows[$i].text
        $cue['terms'] = Get-TermsFor $terms "cue:$movie#$i"
        [void]$list.Add($cue)
    }

    $batch[$movie] = $list
    $cues += $rows.Count
}

if ($batch.Count -gt 0) { $written += Save-Batch $batch 'descriptions' $number }

# --- what a translator has to send back ----------------------------------------------------------

# What "forms" can name, one paragraph per form, so the README explains only the forms this
# language actually owes.
$formNotes = @()
if ($paucal) {
    $formNotes += '      "few"  the paucal, for 2-4, 22-24, ...'
}

if ($largerSingular) {
    $formNotes += @"
      "one"  the SINGULAR, for 21, 31, 101 ... - $Language puts those in the singular, and
             this pair's own singular sentence has no number in it, so it cannot say them.
             Write the number in: this form takes the MANY sentence's placeholders.
"@
}

$countedForms = if ($formNotes.Count -eq 0) {
    @"
  - $Language has two number forms, so there are no ".few" or ".one" keys: every key in your
    output is a key of the input.
"@
} else {
    @"
  - An entry carrying "forms" is the MANY sentence of a counted phrase, and $Language needs
    more number forms than the pair itself has. Write the many key AND the same key plus a
    dot and each named form. Those are the only keys in your output that are not in the input.
$($formNotes -join "`n")
"@
}

$readme = @"
Translating Endless Space 2 Access into $Language
=================================================

These files are INPUT. Do not edit them. For each one, write a NEW file of the same name in
your own output folder, holding only the translations, in the shape below.

strings-<n>.json  ->  a flat key-to-translation object

  {
    "control.button": "<the translation of the button role word>",
    "galaxy.system.outposts.many": "<the MANY form>",
    "galaxy.system.outposts.many.few": "<the paucal form>"
  }

  - Every key of the input part, once, in the input's order.
  - "{0}", "{1}" and friends are slots the mod fills in at runtime. Every placeholder in the
    English has to appear in the translation, and no others. Move them where the grammar
    wants them; never renumber or drop one.
  - "context" says what the string is for and is NOT translated.
  - "terms" is how the game's own $Language files already translate the proper nouns this
    string names. Use those words unless the sentence makes them impossible.
$countedForms

descriptions-<n>.json  ->  a movie-to-array-of-strings object

  {
    "Arctic": ["<cue 1>", "<cue 2>", "<cue 3>", "<cue 4>"]
  }

  - Every movie of the input part, with EXACTLY as many strings as the input has cues, in the
    same order. Each cue is spoken over a few seconds of footage, so a cue that grows into a
    paragraph is spoken over the next one; keep them about as long as the English.

Both files: UTF-8 with no byte order mark, and $Language's own letters written as themselves,
never as \u escapes.

Then, from the repository root:

    .\tools\locale\merge-parts.ps1 -Language $Language -PartsDir <your output folder> -Check
    .\tools\locale\merge-parts.ps1 -Language $Language -PartsDir <your output folder>
"@

$readmePath = Join-Path $target 'README.txt'
Write-Utf8 $readmePath ($readme -replace "`r`n", "`n")

foreach ($path in $written) {
    Write-Host $path.Substring($OutDir.Length).TrimStart('\')
}

Write-Host "README.txt"
Write-Host ""
Write-Host "$((Get-Names $english).Count) strings, $((Get-Names $descriptions).Count) movies into $($written.Count) part(s) under $target."
if ($paucal) {
    Write-Host "$($pairs.Count) counted phrases also need a '.few' form."
}

if ($largerSingular) {
    $owed = @($formsOf.Keys | Where-Object { $formsOf[$_] -contains 'one' })
    Write-Host "$($owed.Count) of them also need a '.one' form."
}

if ($noContext -gt 0) {
    Write-Warning "$noContext key(s) have no comment above their constant and go out with an empty context."
}
