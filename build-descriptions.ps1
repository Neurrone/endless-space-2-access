<#
.SYNOPSIS
    Build the shipped audio-description table from the authoring folder.

.DESCRIPTION
    Turns the per-video description files under descriptions\ into the single table the mod
    loads at runtime, ES2Access\descriptions\<language>.json.

    The authoring folder is NOT in the repository. It is generator output - three files per video,
    hundreds of them - kept locally the way videos\ is, and the built table is the source of truth
    that ships and is committed. This script is how a regenerated or hand-edited authoring folder
    becomes that table again.

    The authoring files carry PLAYER-FACING names ("Vodyani Intro", "Toxic"). The game asks for
    its videos by internal affinity and planet-type codenames ("Vampirilis_Intro", "Swamp"), and
    the mod looks a track up by the basename of the movie the game is actually playing.
    Translating between the two is this script's whole job, so the runtime needs no mapping table
    of its own and cannot drift out of step with one.

    Keys are the movie basename, plus ".LostBack" or ".LostNotBack" for the outros, which is
    exactly how the game itself names a video's sidecar files: InitializeSubtitles builds
    Path.ChangeExtension(moviePath, specifier) (CutsceneModalWindow.cs:196-199).

    Every authoring file must map to a name the game actually has. An unmapped file, or one whose
    .mp4 is missing from the install, fails the build rather than shipping a track that can never
    be found.

    Each cue carries the moment it must be FINISHED by as well as the moment it is spoken. The
    runtime never reads the end - a late cue is spoken anyway, since the mod cannot see the rate
    the player reads at - but it is the only record in the repository of where the video's own
    dialogue resumes, and without it a later rewrite of a cue has nothing to check its length
    against.

.PARAMETER Authoring
    The folder holding the authored descriptions. Defaults to descriptions\ beside this script,
    which a fresh clone does not have.

.PARAMETER Language
    The game's own language name, which names the output file. Defaults to english.

.PARAMETER GameDir
    An Endless Space 2 install to check the resolved names against. Defaults to the GameDir in
    GamePaths.props. Checking is skipped, with a warning, when neither is available.
#>
[CmdletBinding()]
param(
    [string]$Authoring,
    [string]$Language = 'english',
    [string]$GameDir
)

$ErrorActionPreference = 'Stop'

# Not $PSScriptRoot: it is empty inside a param default, which silently turns the defaults below
# into relative paths off whatever the caller's working directory happens to be.
$Root = Split-Path -Parent $MyInvocation.MyCommand.Definition
if (-not $Authoring) { $Authoring = Join-Path $Root 'descriptions' }

# Player-facing faction name -> the affinity codename the game's movie files carry.
$Affinity = @{
    'Cravers'       = 'Cravers'
    'Hissho'        = 'Hisshos'
    'Horatio'       = 'Horatio'
    'Lumeris'       = 'Venetians'
    'Nakalim'       = 'Templars'
    'Riftborn'      = 'Timelords'
    'Sophons'       = 'Sophons'
    'Umbral Choir'  = 'UmbralChoir'
    'Unfallen'      = 'Unfallen'
    'United Empire' = 'Terrans'
    'Vaulters'      = 'Vaulters'
    'Vodyani'       = 'Vampirilis'
}

# The United Empire's three endings are three separate files under one affinity, and Mezari and
# Sheredyn have no intro of their own, so an outro cannot be derived from the affinity alone.
$OutroBase = @{
    'United Empire' = 'Terrans_Outro_UE'
    'Mezari'        = 'Terrans_Outro_Mezari'
    'Sheredyn'      = 'Terrans_Outro_Sheredyn'
}

# Player-facing planet type -> the colonization movie's name. Only the four the game named
# something else, plus the six gas giants it spells as one word.
$PlanetType = @{
    'Gas Burning'   = 'GasBurning'
    'Gas Cold'      = 'GasCold'
    'Gas Frozen'    = 'GasFrozen'
    'Gas Hot'       = 'GasHot'
    'Gas Temperate' = 'GasTemperate'
    'Gas Warm'      = 'GasWarm'
    'Mediterranean' = 'Tropical'
    'Savannah'      = 'Vedt'
    'Steppes'       = 'Steppe'
    'Toxic'         = 'Swamp'
}

# The metaplot outcome the game hands ShowWindow as its subtitlesSpecifier.
$Variant = @{
    'Lost Returned'     = 'LostBack'
    'Lost Not Returned' = 'LostNotBack'
}

# The three metaplot videos. Spelled out rather than derived: unlike an outro, each outcome is a
# whole separate FILE here and the game passes no subtitles specifier for any of them, so the key
# is the plain movie name with no variant on it.
$Metaplot = @{
    'Metaplot (Lost Returned)'         = 'Metaplot_LostBack'
    'Metaplot (Lost Not Returned)'     = 'Metaplot_LostNotBack'
    'Metaplot Victory (Lost Returned)' = 'Metaplot_LostBackVictory'
}

function Resolve-Key {
    param([string]$Folder, [string]$Name)

    switch ($Folder) {
        'colonisation' {
            $movie = if ($PlanetType.ContainsKey($Name)) { $PlanetType[$Name] } else { $Name }
            return @{ Key = $movie; Movie = "Colonization\$movie.mp4" }
        }
        'intros' {
            # An intro is named for its faction alone ("Cravers"), the outros being the only
            # videos a faction has more than one of. The suffix is accepted anyway so a file
            # renamed to match its outro siblings still builds.
            $faction = $Name -replace ' Intro$', ''
            if (-not $Affinity.ContainsKey($faction)) { throw "Unknown faction '$faction' in '$Name'" }
            $movie = "$($Affinity[$faction])_Intro"
            return @{ Key = $movie; Movie = "Factions\$movie.mp4" }
        }
        'outros' {
            if ($Name -notmatch '^(.+) Outro \((.+)\)$') { throw "Not an outro name: '$Name'" }
            $faction = $Matches[1]
            $ending = $Matches[2]
            if (-not $Variant.ContainsKey($ending)) { throw "Unknown ending '$ending' in '$Name'" }
            $movie = if ($OutroBase.ContainsKey($faction)) {
                $OutroBase[$faction]
            }
            elseif ($Affinity.ContainsKey($faction)) {
                "$($Affinity[$faction])_Outro"
            }
            else {
                throw "Unknown faction '$faction' in '$Name'"
            }
            return @{ Key = "$movie.$($Variant[$ending])"; Movie = "Factions\$movie.mp4" }
        }
        'metaplot' {
            if (-not $Metaplot.ContainsKey($Name)) { throw "Unknown metaplot video '$Name'" }
            $movie = $Metaplot[$Name]
            return @{ Key = $movie; Movie = "Metaplot\$movie.mp4" }
        }
        default { throw "Unknown authoring folder '$Folder'" }
    }
}

function ConvertTo-JsonString {
    param([string]$Value)

    $out = New-Object System.Text.StringBuilder
    [void]$out.Append('"')
    foreach ($ch in $Value.ToCharArray()) {
        if ($ch -eq '"') { [void]$out.Append('\"') }
        elseif ($ch -eq '\') { [void]$out.Append('\\') }
        elseif ([int]$ch -eq 8) { [void]$out.Append('\b') }
        elseif ([int]$ch -eq 12) { [void]$out.Append('\f') }
        elseif ([int]$ch -eq 10) { [void]$out.Append('\n') }
        elseif ([int]$ch -eq 13) { [void]$out.Append('\r') }
        elseif ([int]$ch -eq 9) { [void]$out.Append('\t') }
        elseif ([int]$ch -lt 0x20) { [void]$out.AppendFormat('\u{0:x4}', [int]$ch) }
        else { [void]$out.Append($ch) }
    }
    [void]$out.Append('"')
    return $out.ToString()
}

if (-not (Test-Path $Authoring)) {
    throw "No authoring folder at $Authoring"
}

if (-not $GameDir) {
    $props = Join-Path $Root 'GamePaths.props'
    if (Test-Path $props) {
        $found = Select-String -Path $props -Pattern '<GameDir>(.+)</GameDir>' | Select-Object -First 1
        if ($found) { $GameDir = $found.Matches[0].Groups[1].Value }
    }
}

$movies = $null
if ($GameDir -and (Test-Path $GameDir)) {
    $movies = Join-Path $GameDir 'EndlessSpace2_Data\StreamingAssets\Movies'
    if (-not (Test-Path $movies)) {
        Write-Warning "No Movies folder under $GameDir; resolved names go unchecked."
        $movies = $null
    }
}
else {
    Write-Warning 'No game install found; resolved names go unchecked.'
}

$tracks = @{}
$cueCount = 0
$wordCount = 0

foreach ($file in Get-ChildItem -Path $Authoring -Filter *.json -Recurse | Sort-Object FullName) {
    $folder = Split-Path -Leaf $file.DirectoryName
    $name = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
    $resolved = Resolve-Key -Folder $folder -Name $name

    if ($tracks.ContainsKey($resolved.Key)) {
        throw "Two authoring files both resolve to '$($resolved.Key)'; the second is $($file.FullName)"
    }

    if ($movies) {
        $mp4 = Join-Path $movies $resolved.Movie
        if (-not (Test-Path $mp4)) {
            throw "'$name' resolves to $($resolved.Movie), which this install does not have"
        }
    }

    $authored = Get-Content -Raw -Encoding UTF8 $file.FullName | ConvertFrom-Json
    $duration = [double]$authored.meta.duration

    $cues = @()
    $previous = -1.0
    foreach ($cue in $authored.cues) {
        $at = [double]$cue.start
        $end = [double]$cue.end
        if ($at -lt $previous) {
            throw "'$name' has cues out of order at $at seconds"
        }
        if ($at -gt $duration) {
            throw "'$name' has a cue at $at seconds, past its $duration second runtime"
        }
        if ($end -lt $at) {
            throw "'$name' has a cue at $at seconds that ends at $end, before it is spoken"
        }
        $previous = $at
        $text = ($cue.text -replace '\s+', ' ').Trim()
        $cues += @{ At = $at; End = $end; Text = $text }
        $wordCount += ($text -split ' ').Count
    }

    if ($cues.Count -eq 0) {
        Write-Warning "'$name' has no cues; skipped."
        continue
    }

    $cueCount += $cues.Count
    $tracks[$resolved.Key] = $cues
}

$out = New-Object System.Text.StringBuilder
[void]$out.AppendLine('{')
$keys = @($tracks.Keys | Sort-Object)
for ($i = 0; $i -lt $keys.Count; $i++) {
    $key = $keys[$i]
    [void]$out.AppendLine("  $(ConvertTo-JsonString $key): [")
    $cues = $tracks[$key]
    for ($j = 0; $j -lt $cues.Count; $j++) {
        $at = [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0:0.###}', $cues[$j].At)
        $end = [string]::Format([System.Globalization.CultureInfo]::InvariantCulture, '{0:0.###}', $cues[$j].End)
        $text = ConvertTo-JsonString $cues[$j].Text
        $tail = if ($j -lt $cues.Count - 1) { ',' } else { '' }
        [void]$out.AppendLine("    { ""at"": $at, ""end"": $end, ""text"": $text }$tail")
    }
    $tail = if ($i -lt $keys.Count - 1) { ',' } else { '' }
    [void]$out.AppendLine("  ]$tail")
}
[void]$out.AppendLine('}')

$outFile = Join-Path $Root "ES2Access\descriptions\$Language.json"
$outDir = Split-Path -Parent $outFile
if (-not (Test-Path $outDir)) { [void](New-Item -ItemType Directory -Path $outDir) }
[System.IO.File]::WriteAllText($outFile, $out.ToString(), (New-Object System.Text.UTF8Encoding($false)))

Write-Host "$($tracks.Count) tracks, $cueCount cues, $wordCount words -> $outFile"
