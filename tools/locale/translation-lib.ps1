<#
.SYNOPSIS
What the batch export and the merge both have to know: how to read the mod's own string
table, what each string is FOR, which strings are counted, and how to write JSON a player's
file can be made of.

Dot-sourced by export-batches.ps1 and merge-parts.ps1; not run on its own.
#>

$ErrorActionPreference = 'Stop'

# The languages the game ships in, under its own names, which are also the file names. Kept in
# step with ES2Access.Tests\Speech\TranslationFiles.cs, which is what actually lints the files.
$script:GameLanguages = @(
    'brazilian', 'french', 'german', 'koreana', 'polish',
    'russian', 'schinese', 'spanish', 'tchinese'
)

# The languages with a third, paucal form, which is the only reason a ".few" key exists.
$script:ThreeFormLanguages = @('polish', 'russian')

# The suffix a paucal form hangs off its pair's MANY key (PluralRules.FewSuffix).
$script:FewSuffix = '.few'

function Test-GameLanguage([string]$language) {
    return $script:GameLanguages -contains $language
}

function Test-ThreeForm([string]$language) {
    return $script:ThreeFormLanguages -contains $language
}

function Get-GameLanguages {
    return $script:GameLanguages
}

# Property order survives ConvertFrom-Json, and english.json's order is the order a translator
# reads in, so every file this pair writes is written in it.
function Read-JsonOrdered([string]$path) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Not found: $path" }
    return ConvertFrom-Json ([System.IO.File]::ReadAllText($path))
}

function Get-Names($object) {
    return @($object.PSObject.Properties | ForEach-Object { $_.Name })
}

function Test-HasName($object, [string]$name) {
    return $null -ne $object.PSObject.Properties[$name]
}

# --- JSON out -------------------------------------------------------------------------------
# Hand-written rather than ConvertTo-Json: Windows PowerShell escapes every non-ASCII character
# to \uXXXX, and a translation file full of ł is unreadable to the next translator and to
# the encoding lint alike.

function Format-JsonString([string]$text) {
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
    return '"' + $out.ToString() + '"'
}

# Cue timings belong to the footage, so they are written back exactly as English holds them.
function Format-JsonNumber($value) {
    return ([double]$value).ToString('R', [System.Globalization.CultureInfo]::InvariantCulture)
}

<#
.SYNOPSIS
A value as pretty JSON: ordered dictionaries become objects in insertion order, arrays become
arrays, everything else becomes a scalar. Two-space indent, no trailing newline.
#>
function ConvertTo-JsonText($value, [int]$level = 0) {
    $pad = '  ' * $level
    $inner = '  ' * ($level + 1)
    if ($null -eq $value) { return 'null' }
    if ($value -is [string]) { return Format-JsonString $value }
    if ($value -is [bool]) { if ($value) { return 'true' } else { return 'false' } }
    if ($value -is [System.Collections.IDictionary]) {
        $items = @()
        foreach ($name in $value.Keys) {
            $items += $inner + (Format-JsonString ([string]$name)) + ': ' +
                (ConvertTo-JsonText $value[$name] ($level + 1))
        }
        if ($items.Count -eq 0) { return '{}' }
        return "{`n" + ($items -join ",`n") + "`n$pad}"
    }
    if ($value -is [System.Collections.IEnumerable]) {
        $items = @()
        foreach ($item in $value) {
            $items += $inner + (ConvertTo-JsonText $item ($level + 1))
        }
        if ($items.Count -eq 0) { return '[]' }
        return "[`n" + ($items -join ",`n") + "`n$pad]"
    }
    return Format-JsonNumber $value
}

function Write-Utf8([string]$path, [string]$body) {
    $dir = Split-Path -Parent $path
    if ($dir -and -not (Test-Path -LiteralPath $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
    [System.IO.File]::WriteAllText($path, $body, (New-Object System.Text.UTF8Encoding($false)))
}

# --- What each string is for ----------------------------------------------------------------

function Clear-CommentMarkup([string]$line) {
    $text = $line -replace '^\s*///?', ''
    $text = $text -replace '</?summary>', ''
    $text = $text -replace '</?remarks>', ''
    $text = $text -replace '<see\s+cref="[A-Za-z0-9_.]*?([A-Za-z0-9_]+)"\s*/>', '$1'
    $text = $text -replace '<see\s+langword="([A-Za-z0-9_]+)"\s*/>', '$1'
    $text = $text -replace '<paramref\s+name="([A-Za-z0-9_]+)"\s*/>', '$1'
    $text = $text -replace '</?c>', ''
    $text = $text -replace '</?para>', ' '
    return $text
}

function Compress-Whitespace([string]$text, [int]$cap = 400) {
    $one = ($text -replace '\s+', ' ').Trim()
    if ($one.Length -gt $cap) { $one = $one.Substring(0, $cap).TrimEnd() }
    return $one
}

<#
.SYNOPSIS
Every locale key ModStrings*.cs names, the constant that names it where there is one, and the
prose above it that says what the string is for.

.DESCRIPTION
A key is spotted as a string literal that english.json answers for, which finds the ones
declared as constants and the ones listed in a table (the action names are a table, because
they are composed from the input layer's own names rather than declared one at a time).

Its context is the comment block immediately above it; failing that the last comment block
seen among the keys around it, which is how one group comment covers the run under it; failing
that the comment above the enclosing class, which is what the icon and action tables have
instead of a line each. A method declaration ends a run - a method's doc comment describes the
method, not the keys inside it - and a class declaration starts a new one.

Entries marked IsPrefix are not keys but the stems keys are built from ("color."), and carry
the only prose the keys built that way have.
#>
function Get-ModStringsEntries([string]$root, $englishKeys) {
    $entries = New-Object System.Collections.ArrayList
    $seen = New-Object 'System.Collections.Generic.HashSet[string]'
    $literal = [regex]'"([^"\\]*)"'
    $folder = Join-Path $root 'ES2Access\Core\Speech'
    foreach ($file in (Get-ChildItem -LiteralPath $folder -Filter 'ModStrings*.cs' | Sort-Object Name)) {
        $pending = ''
        $group = ''
        $scope = ''
        $field = ''
        $block = New-Object System.Collections.ArrayList
        $inComment = $false
        foreach ($line in [System.IO.File]::ReadAllLines($file.FullName)) {
            $text = $line.Trim()
            if ($text -eq '') { continue }
            if ($text.StartsWith('//')) {
                if (-not $inComment) { $block.Clear(); $inComment = $true }
                [void]$block.Add((Clear-CommentMarkup $text))
                continue
            }

            if ($inComment) {
                $inComment = $false
                $joined = Compress-Whitespace ($block -join ' ')
                $pending = $joined
                $group = $joined
            }

            # Both patterns bar quotes and '=' on purpose: "public const string IconHeroClass =
            # \"icon.hero-class\";" is a key, and the word "class" inside its name and its value
            # must not make it look like a type declaration.
            if ($text -match '^(public|internal|private|protected)[A-Za-z ]*\b(class|struct|enum|interface)\s+\w') {
                if ($pending -ne '') { $scope = $pending }
                $pending = ''
                $group = ''
                $field = ''
                continue
            }

            if ($text -match '^(public|internal|private|protected)[^="]*\(') {
                $pending = ''
                $group = ''
                $field = ''
                continue
            }

            # A constant whose value is on the next line still names its key; remember whose key
            # the literal below is about to be.
            if ($text -match '^public const string\s+(\w+)\s*=') { $field = $Matches[1] }

            $first = $true
            foreach ($match in $literal.Matches($text)) {
                $key = $match.Groups[1].Value
                $isKey = Test-HasName $englishKeys $key
                # A key ending in a dot is a PREFIX the mod glues a name onto - the colour names
                # are built that way - so the comment above it is all the context those keys have.
                $isPrefix = -not $isKey -and $key -match '^[a-z][a-z0-9.-]*\.$'
                if (-not $isKey -and -not $isPrefix) { continue }
                if (-not $seen.Add($key)) {
                    $field = ''
                    continue
                }

                $context = $pending
                if ($context -eq '') { $context = $group }
                if ($context -eq '') { $context = $scope }
                [void]$entries.Add([pscustomobject]@{
                    Field    = $field
                    Key      = $key
                    Context  = $context
                    IsPrefix = $isPrefix
                })

                $field = ''
                if ($first) { $pending = ''; $first = $false }
            }
        }
    }

    return $entries
}

# --- Which strings are counted ----------------------------------------------------------------

# SystemLabelReadout.AddShipCount takes its pair as parameters, so no scan can see it; its only
# caller passes these two constants. Mirrors ES2Access.Tests\Speech\PluralPairs.cs, which is
# what fails the build if a THIRD indirect call site ever appears.
$script:TracedPluralFields = @('GalaxySystemFriendlyShips', 'GalaxySystemHostileShips')
$script:TracedPluralSite = 'ES2Access/UI/SystemLabelReadout.cs'

<#
.SYNOPSIS
The MANY key of every counted pair the mod speaks - the keys a three-form language owes a
paucal form. Read off the ModStrings.Plural call sites, because the pairs are named every way
English suggests and no naming convention can find them.
#>
function Get-PluralManyKeys([string]$root, $fieldToKey) {
    $keys = New-Object 'System.Collections.Generic.SortedSet[string]'
    $call = [regex]'ModStrings\.Plural\(\s*([A-Za-z_][\w.]*)\s*,\s*([A-Za-z_][\w.]*)\s*,'
    $qualifier = 'ModStrings.'
    $strays = New-Object 'System.Collections.Generic.SortedSet[string]'
    foreach ($file in (Get-ChildItem -LiteralPath (Join-Path $root 'ES2Access') -Recurse -Filter '*.cs')) {
        $relative = $file.FullName.Substring($root.Length).TrimStart('\').Replace('\', '/')
        foreach ($match in $call.Matches([System.IO.File]::ReadAllText($file.FullName))) {
            $argument = $match.Groups[2].Value
            if (-not $argument.StartsWith($qualifier)) {
                if ($relative -ne $script:TracedPluralSite) { [void]$strays.Add($relative) }
                continue
            }

            $field = $argument.Substring($qualifier.Length)
            if (-not $fieldToKey.ContainsKey($field)) {
                throw "$relative : ModStrings.$field is not a string constant this scan can see."
            }

            [void]$keys.Add($fieldToKey[$field])
        }
    }

    foreach ($field in $script:TracedPluralFields) {
        if (-not $fieldToKey.ContainsKey($field)) {
            throw "$($script:TracedPluralSite) : ModStrings.$field is gone; retrace the pair."
        }

        [void]$keys.Add($fieldToKey[$field])
    }

    foreach ($stray in $strays) {
        Write-Warning "$stray calls Plural with something this scan cannot resolve; its pair may be missing a paucal form."
    }

    return $keys
}

# --- The glossary ----------------------------------------------------------------------------

<#
.SYNOPSIS
How the game itself already translates the terms a string mentions: a lookup from a
terms.json reference ("locale:<key>", "cue:<Movie>#<index>") to the terms it names, and the
language's top translation of each.
#>
function Get-TermIndex([string]$root, [string]$language) {
    $termsPath = Join-Path $root 'tools\glossary\out\terms.json'
    $glossaryPath = Join-Path $root "tools\glossary\out\$language.json"
    $index = @{ ByReference = @{}; Translation = @{} }
    if (-not (Test-Path -LiteralPath $termsPath) -or -not (Test-Path -LiteralPath $glossaryPath)) {
        Write-Warning "No glossary for $language under tools\glossary\out; batches will carry no terms."
        return $index
    }

    $terms = Read-JsonOrdered $termsPath
    $glossary = Read-JsonOrdered $glossaryPath
    foreach ($property in $terms.PSObject.Properties) {
        $term = $property.Name
        $rows = @()
        if (Test-HasName $glossary $term) { $rows = @($glossary.$term) }
        if ($rows.Count -eq 0) { continue }
        $translation = [string]$rows[0].text
        if ($translation -eq '') { continue }
        $index.Translation[$term] = $translation
        foreach ($reference in @($property.Value)) {
            if (-not $index.ByReference.ContainsKey($reference)) {
                $index.ByReference[$reference] = New-Object System.Collections.ArrayList
            }

            [void]$index.ByReference[$reference].Add($term)
        }
    }

    return $index
}

# At most this many terms per string: more is noise, and nothing in the glossary comes close.
$script:TermCap = 8

<#
.SYNOPSIS
The terms one string mentions, as an ordered term-to-translation table. Longest term first, so
if the cap ever bites it drops the vaguest ones.
#>
function Get-TermsFor($index, [string]$reference) {
    $table = New-Object System.Collections.Specialized.OrderedDictionary
    if (-not $index.ByReference.ContainsKey($reference)) { return $table }
    $terms = @($index.ByReference[$reference] |
        Sort-Object -Property @{ Expression = { $_.Length }; Descending = $true }, @{ Expression = { $_ } } |
        Select-Object -First $script:TermCap)
    foreach ($term in $terms) { $table[$term] = $index.Translation[$term] }
    return $table
}

# --- Placeholders -----------------------------------------------------------------------------

function Get-Placeholders([string]$text) {
    $found = New-Object 'System.Collections.Generic.SortedSet[string]'
    foreach ($match in ([regex]'\{\d+\}').Matches($text)) { [void]$found.Add($match.Value) }
    return ($found -join ' ')
}

function Get-BaseKey([string]$key) {
    if ($key.EndsWith($script:FewSuffix)) {
        return $key.Substring(0, $key.Length - $script:FewSuffix.Length)
    }

    return $key
}

function Test-Paucal([string]$key) {
    return $key.Length -gt $script:FewSuffix.Length -and $key.EndsWith($script:FewSuffix)
}

function Get-FewSuffix { return $script:FewSuffix }
