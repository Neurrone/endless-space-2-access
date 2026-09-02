<#
    extract-glossary.ps1 - builds a per-language glossary of the game's own wording for the
    game concepts the mod's strings talk about, so a translator of the mod uses Amplitude's
    official term (Dust, Influence, Approval, Manpower, faction and planet names, screen
    titles) instead of inventing one.

    Reads, never writes:
      <GameDir>\Public\Localization\<language>\*.xml   the shipped localization tables
      ES2Access\locale\english.json                    the mod strings awaiting translation
      ES2Access\descriptions\english.json              the mod's video cue texts and movie keys

    Writes into <OutDir> (default tools\glossary\out, gitignored):
      <language>.json  English game term -> up to 3 official translations, each with how many
                       localization keys carry it and up to 5 of those %Keys
      terms.json       English game term -> the mod keys and cues that mention it, so a
                       translation batch can pull only the terms its keys need

    The output is a translation aid only. It is not loaded at runtime and the game's text is
    never committed.
#>
[CmdletBinding()]
param(
    [string]$GameDir = 'C:\Program Files (x86)\Steam\steamapps\common\Endless Space 2',
    [string]$OutDir
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $OutDir) { $OutDir = Join-Path $PSScriptRoot 'out' }

$localizationRoot = Join-Path $GameDir 'Public\Localization'
if (-not (Test-Path $localizationRoot)) { throw "Localization folder not found: $localizationRoot" }

# --- text handling -----------------------------------------------------------------------

$markup = @(
    '\{[0-9]+\}',                       # {0} placeholders
    '\$[A-Za-z_][A-Za-z0-9_]*',         # $HeroName style substitutions
    '#REVERT#',
    '#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?#',   # #RRGGBBAA# colours
    '\[[^\]]{1,40}\]',                  # [turn], [dustColored] icon tokens
    '<[^>]{1,40}>'                      # any html-ish tag
) -join '|'
$markupRegex = [regex]$markup
$spaceRegex = [regex]'\s+'
$edgeRegex = [regex]'^[\s\p{P}]+|[\s\p{P}]+$'
$wordRegex = [regex]'[^\p{L}\p{N}]+'
$camelRegex = [regex]'(?<=[\p{Ll}0-9])(?=\p{Lu})'

function Get-StrippedText([string]$text) {
    if (-not $text) { return '' }
    $t = $markupRegex.Replace($text, ' ')
    $t = $spaceRegex.Replace($t, ' ')
    $t.Trim()
}

# Term identity for matching: lowercase words joined by single spaces.
function Get-Normalized([string]$text) {
    if (-not $text) { return '' }
    $t = $edgeRegex.Replace($text, '')
    $t = $wordRegex.Replace($t.ToLowerInvariant(), ' ')
    $t.Trim()
}

# Single common words that would only add noise; multi-word phrases containing them are kept.
$stopWords = New-Object 'System.Collections.Generic.HashSet[string]'
foreach ($w in @(
    'a','an','and','any','all','are','as','at','be','been','being','both','but','by','can','could',
    'do','does','done','each','either','else','ever','every','for','from','had','has','have','he',
    'her','here','hers','him','his','how','i','if','in','into','is','it','its','just','like','may',
    'me','might','more','most','much','must','my','need','no','none','nor','not','now','of','off',
    'on','once','one','only','or','other','others','our','out','over','own','per','same','see',
    'shall','she','should','so','some','such','than','that','the','their','them','then','there',
    'these','they','this','those','through','thus','to','too','two','under','until','up','upon',
    'us','use','used','very','was','we','were','what','when','where','which','while','who','why',
    'will','with','would','yet','you','your','yours'
)) { [void]$stopWords.Add($w) }

# --- corpus: everything the mod's own strings say ----------------------------------------

$modLocalePath = Join-Path $repoRoot 'ES2Access\locale\english.json'
$modDescPath = Join-Path $repoRoot 'ES2Access\descriptions\english.json'

$ngramRefs = @{}    # normalized 1..4-word phrase -> list of mod references that contain it

function Add-CorpusText([string]$text, [string]$reference) {
    $norm = Get-Normalized (Get-StrippedText $text)
    if (-not $norm) { return }
    $words = $norm.Split(' ')
    for ($i = 0; $i -lt $words.Length; $i++) {
        $limit = [Math]::Min(4, $words.Length - $i)
        for ($n = 1; $n -le $limit; $n++) {
            $gram = [string]::Join(' ', $words, $i, $n)
            $refs = $ngramRefs[$gram]
            if ($null -eq $refs) {
                $refs = New-Object System.Collections.ArrayList
                $ngramRefs[$gram] = $refs
            }
            if ($refs.Count -lt 24 -and -not $refs.Contains($reference)) { [void]$refs.Add($reference) }
        }
    }
}

Write-Host 'Reading mod strings...'
$modLocale = (Get-Content -LiteralPath $modLocalePath -Raw -Encoding UTF8) -replace '^\uFEFF', '' | ConvertFrom-Json
foreach ($p in $modLocale.PSObject.Properties) { Add-CorpusText $p.Value ("locale:" + $p.Name) }

$modDesc = (Get-Content -LiteralPath $modDescPath -Raw -Encoding UTF8) -replace '^\uFEFF', '' | ConvertFrom-Json
foreach ($movie in $modDesc.PSObject.Properties) {
    # The movie key itself is a game term (planet types, faction codenames).
    $keyWords = $camelRegex.Replace(($movie.Name -replace '_', ' '), ' ')
    Add-CorpusText $keyWords ("movie:" + $movie.Name)
    $i = 0
    foreach ($cue in $movie.Value) {
        Add-CorpusText $cue.text ("cue:" + $movie.Name + "#" + $i)
        $i++
    }
}
Write-Host ("  corpus phrases: {0}" -f $ngramRefs.Count)

# --- the game's localization tables -------------------------------------------------------

function Read-Language([string]$languageDir) {
    $table = @{}
    foreach ($file in Get-ChildItem -LiteralPath $languageDir -Filter '*.xml' -File) {
        $xml = New-Object System.Xml.XmlDocument
        $xml.PreserveWhitespace = $false
        $xml.Load($file.FullName)
        foreach ($pair in $xml.SelectNodes('//LocalizationPair')) {
            $name = $pair.GetAttribute('Name')
            if ($name) { $table[$name] = Get-StrippedText $pair.InnerText }
        }
    }
    $table
}

$languages = Get-ChildItem -LiteralPath $localizationRoot -Directory | Select-Object -ExpandProperty Name
if ($languages -notcontains 'english') { throw "No english localization under $localizationRoot" }

Write-Host 'Reading english localization...'
$english = Read-Language (Join-Path $localizationRoot 'english')
Write-Host ("  english keys: {0}" -f $english.Count)

# --- candidate terms ----------------------------------------------------------------------

# normalized term -> @{ Display = ...; Counts = @{ raw -> n }; Keys = ArrayList }
$terms = @{}
foreach ($entry in $english.GetEnumerator()) {
    $raw = $entry.Value
    if (-not $raw) { continue }
    $norm = Get-Normalized $raw
    if (-not $norm) { continue }
    $words = $norm.Split(' ')
    if ($words.Length -lt 1 -or $words.Length -gt 4) { continue }
    if ($words.Length -eq 1) {
        if ($stopWords.Contains($words[0])) { continue }
        if ($words[0].Length -lt 3) { continue }
        if ($words[0] -match '^[0-9]+$') { continue }
        # Precision guard: a one-word game concept is a label, and labels are capitalised.
        if ($raw -notmatch '^\P{L}*\p{Lu}') { continue }
    }
    if ($norm -notmatch '\p{L}') { continue }
    if (-not $ngramRefs.ContainsKey($norm)) { continue }

    $term = $terms[$norm]
    if ($null -eq $term) {
        $term = @{ Counts = @{}; Keys = (New-Object System.Collections.ArrayList) }
        $terms[$norm] = $term
    }
    $display = $edgeRegex.Replace($raw, '')
    if (-not $display) { $display = $raw }
    if ($term.Counts.ContainsKey($display)) { $term.Counts[$display]++ } else { $term.Counts[$display] = 1 }
    [void]$term.Keys.Add($entry.Key)
}

# ALL-CAPS spellings are the game shouting in a header, not the term's normal form.
function Test-AllCaps([string]$s) {
    ($s -notmatch '\p{Ll}') -and ($s -match '\p{Lu}.*\p{Lu}')
}

# Pick the spelling to show for a set of case variants: normal case first, then the commonest.
function Select-Representative($counts) {
    $best = $null; $bestCaps = 2; $bestCount = -1
    foreach ($c in $counts.GetEnumerator()) {
        $caps = if (Test-AllCaps $c.Key) { 1 } else { 0 }
        $better = ($caps -lt $bestCaps) -or
                  ($caps -eq $bestCaps -and ($c.Value -gt $bestCount -or
                   ($c.Value -eq $bestCount -and [string]::CompareOrdinal($c.Key, $best) -lt 0)))
        if ($better) { $best = $c.Key; $bestCaps = $caps; $bestCount = $c.Value }
    }
    $best
}

foreach ($term in $terms.Values) {
    $term.Display = Select-Representative $term.Counts
    $term.Keys.Sort()
}
Write-Host ("  candidate terms: {0}" -f $terms.Count)

$orderedNorms = $terms.Keys | Sort-Object { $terms[$_].Display } -CaseSensitive:$false

# --- json writing (hand-rolled so translations stay readable, not \uXXXX) ------------------

$ctrlRegex = [regex]'[\x00-\x1f]'
function ConvertTo-JsonString([string]$s) {
    $s = $s.Replace('\', '\\').Replace('"', '\"')
    $s = $ctrlRegex.Replace($s, { param($m) '\u{0:x4}' -f [int][char]$m.Value })
    '"' + $s + '"'
}

function Write-Utf8NoBom([string]$path, [string]$content) {
    [System.IO.File]::WriteAllText($path, $content, (New-Object System.Text.UTF8Encoding($false)))
}

if (-not (Test-Path $OutDir)) { [void](New-Item -ItemType Directory -Path $OutDir -Force) }

# --- terms.json ----------------------------------------------------------------------------

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('{')
$first = $true
foreach ($norm in $orderedNorms) {
    $refs = $ngramRefs[$norm]
    if (-not $first) { [void]$sb.AppendLine(',') }
    $first = $false
    [void]$sb.Append('  ').Append((ConvertTo-JsonString $terms[$norm].Display)).Append(': [')
    $sep = ''
    foreach ($r in $refs) { [void]$sb.Append($sep).Append((ConvertTo-JsonString $r)); $sep = ', ' }
    [void]$sb.Append(']')
}
[void]$sb.AppendLine()
[void]$sb.AppendLine('}')
Write-Utf8NoBom (Join-Path $OutDir 'terms.json') $sb.ToString()

# --- per-language glossaries ----------------------------------------------------------------

$ties = @{}
foreach ($language in $languages) {
    Write-Host ("Building {0}..." -f $language)
    $table = if ($language -eq 'english') { $english } else { Read-Language (Join-Path $localizationRoot $language) }

    $sb = New-Object System.Text.StringBuilder
    [void]$sb.AppendLine('{')
    $firstTerm = $true
    foreach ($norm in $orderedNorms) {
        $term = $terms[$norm]
        # Group translations case-insensitively so a shouted header is not a rival wording.
        $counts = @{}       # lowercased translation -> total keys carrying it
        $variants = @{}     # lowercased translation -> @{ spelling -> n }
        $keysFor = @{}      # lowercased translation -> up to 5 english %Keys
        foreach ($key in $term.Keys) {
            $translated = $table[$key]
            if (-not $translated) { continue }
            # Label punctuation ("Multiplier:") is not part of the term.
            $translated = $edgeRegex.Replace($translated, '')
            if (-not $translated) { continue }
            $fold = $translated.ToLowerInvariant()
            if ($counts.ContainsKey($fold)) { $counts[$fold]++ } else {
                $counts[$fold] = 1
                $variants[$fold] = @{}
                $keysFor[$fold] = New-Object System.Collections.ArrayList
            }
            if ($variants[$fold].ContainsKey($translated)) { $variants[$fold][$translated]++ } else { $variants[$fold][$translated] = 1 }
            if ($keysFor[$fold].Count -lt 5) { [void]$keysFor[$fold].Add($key) }
        }
        if ($counts.Count -eq 0) { continue }
        $top = $counts.GetEnumerator() | Sort-Object @{ Expression = { $_.Value }; Descending = $true }, @{ Expression = { $_.Key } } | Select-Object -First 3
        if ($top.Count -ge 2 -and $top[0].Value -eq $top[1].Value -and $top[0].Value -ge 2) {
            if (-not $ties.ContainsKey($term.Display)) { $ties[$term.Display] = (New-Object System.Collections.ArrayList) }
            [void]$ties[$term.Display].Add($language)
        }

        if (-not $firstTerm) { [void]$sb.AppendLine(',') }
        $firstTerm = $false
        [void]$sb.Append('  ').Append((ConvertTo-JsonString $term.Display)).AppendLine(': [')
        $firstEntry = $true
        foreach ($t in $top) {
            if (-not $firstEntry) { [void]$sb.AppendLine(',') }
            $firstEntry = $false
            [void]$sb.Append('    { "text": ').Append((ConvertTo-JsonString (Select-Representative $variants[$t.Key])))
            [void]$sb.Append(', "count": ').Append($t.Value).Append(', "keys": [')
            $sep = ''
            foreach ($k in $keysFor[$t.Key]) { [void]$sb.Append($sep).Append((ConvertTo-JsonString $k)); $sep = ', ' }
            [void]$sb.Append('] }')
        }
        [void]$sb.AppendLine()
        [void]$sb.Append('  ]')
    }
    [void]$sb.AppendLine()
    [void]$sb.AppendLine('}')
    Write-Utf8NoBom (Join-Path $OutDir ($language + '.json')) $sb.ToString()
}

if ($ties.Count -gt 0) {
    Write-Host ''
    Write-Host ("Terms with two equally common translations in at least one language ({0}); a human should pick:" -f $ties.Count)
    foreach ($t in ($ties.Keys | Sort-Object)) {
        Write-Host ("  {0}  [{1}]" -f $t, ($ties[$t] -join ', '))
    }
}
Write-Host ''
Write-Host ("Wrote {0} terms to {1}" -f $orderedNorms.Count, $OutDir)
