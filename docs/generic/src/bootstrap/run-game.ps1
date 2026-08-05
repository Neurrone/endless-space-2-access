param(
    [switch]$NoBuild,
    [switch]$NoSpeech,
    [switch]$NoDev,
    [switch]$NoWait,
    # Boot straight into a saved game, skipping the main menu. The value is the save's title
    # (not a file path), matched case-insensitively; empty is not accepted here, but POST
    # /loadsave with an empty body takes the most recent save.
    [string]$LoadSave
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$devUrl = 'http://127.0.0.1:8771'
$devPort = 8771
$lockPath = Join-Path $env:TEMP 'es2access-run-game.lock'

if ($LoadSave -and $NoDev) {
    Write-Error "-LoadSave drives the dev server's POST /loadsave, so it cannot be used with -NoDev."
    exit 1
}

# --- one game at a time -------------------------------------------------------------------
# Two copies of Endless Space 2 fight over one dev port, and the loser wins silently: the
# second game's mod finds 8771 taken, logs it, and carries on without a dev server - so every
# request an agent then makes is answered by the FIRST game, whose state has nothing to do with
# what the test just did. That failure looks like a mod bug and takes an hour to disbelieve. So
# a launch refuses rather than allows it, and never kills anything: a running game may be
# something the developer is in the middle of.

# Amplitude's own launcher (launcher-x64.exe) is a separate process that outlives handing off to
# the game, so it counts too. Named precisely rather than by "anything with launcher in it":
# every other game store on the machine has a process called something-Launcher, and matching one
# of those would refuse a launch for no reason.
$gameProcessNames = '^(EndlessSpace2|launcher-x64|launcher-x86)$'

function Get-LiveGameProcess {
    # Same-session only: a launcher orphaned into the Services session (session 0) is not a game
    # this script could have started, cannot be killed from here, and would otherwise block every
    # future launch forever.
    $session = (Get-Process -Id $PID).SessionId
    Get-Process -ErrorAction SilentlyContinue |
        Where-Object { $_.ProcessName -match $gameProcessNames -and $_.SessionId -eq $session }
}

function Test-DevPortBound {
    try {
        $null -ne (Get-NetTCPConnection -LocalPort $devPort -State Listen -ErrorAction Stop)
    } catch {
        $false
    }
}

if (Test-Path $lockPath) {
    $lockedPid = 0
    if ([int]::TryParse((Get-Content $lockPath -Raw -ErrorAction SilentlyContinue).Trim(), [ref]$lockedPid)) {
        $holder = Get-Process -Id $lockedPid -ErrorAction SilentlyContinue
        # A dead pid, or one Windows has since handed to something else, is a lock left behind by
        # a run that crashed. Clearing it is safe precisely because the identity is checked.
        if ($holder -and $holder.ProcessName -match $gameProcessNames) {
            Write-Error "Endless Space 2 is already running as pid $lockedPid (lock: $lockPath). Quit it first - POST $devUrl/quit - or delete the lock if that pid is not the game."
            exit 1
        }
    }
    Remove-Item $lockPath -Force -ErrorAction SilentlyContinue
}

# A game that is on its way out holds the port for a few seconds after POST /quit answers, which
# is the normal case for a test loop relaunching immediately. Waiting is right; killing is not.
$deadline = (Get-Date).AddSeconds(15)
while ((Get-LiveGameProcess) -or (Test-DevPortBound)) {
    if ((Get-Date) -gt $deadline) {
        $running = (Get-LiveGameProcess | ForEach-Object { "$($_.ProcessName) (pid $($_.Id))" }) -join ', '
        $portMsg = if (Test-DevPortBound) { "port $devPort is still listening" } else { 'the port is free' }
        Write-Error "Endless Space 2 has not finished shutting down after 15s: $portMsg; processes: $(if ($running) { $running } else { 'none' }). Nothing was launched and nothing was killed - quit the running game yourself, then retry."
        exit 1
    }
    Write-Host "Waiting for the previous game to exit..."
    Start-Sleep -Seconds 1
}

if (-not $NoBuild) {
    dotnet build "$root\ES2Access\ES2Access.csproj"
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$props = [xml](Get-Content "$root\GamePaths.props")
$gameDir = ($props.Project.PropertyGroup | Where-Object { $_.GameDir } | Select-Object -First 1).GameDir

if ($NoSpeech) { $env:ES2ACCESS_NO_SPEECH = '1' } else { $env:ES2ACCESS_NO_SPEECH = $null }

# The dev server is opt-in via BepInEx config (off for players); dev runs opt in here.
$cfgPath = Join-Path $gameDir 'BepInEx\config\endless.space2.access.cfg'
$devValue = if ($NoDev) { 'false' } else { 'true' }
if (Test-Path $cfgPath) {
    $cfgText = Get-Content $cfgPath -Raw
    if ($cfgText -match '(?m)^\s*devServer\s*=') {
        $cfgText = $cfgText -replace '(?m)^\s*devServer\s*=.*$', "devServer = $devValue"
    } elseif ($cfgText -match '(?m)^\[Dev\]') {
        $cfgText = $cfgText -replace '(?m)^\[Dev\]\s*$', "[Dev]`r`ndevServer = $devValue"
    } else {
        $cfgText += "`r`n[Dev]`r`ndevServer = $devValue`r`n"
    }
    Set-Content $cfgPath $cfgText -Encoding utf8
} else {
    New-Item -ItemType Directory -Force (Split-Path $cfgPath) | Out-Null
    Set-Content $cfgPath "[Dev]`r`ndevServer = $devValue`r`n" -Encoding utf8
}

$proc = Start-Process "$gameDir\EndlessSpace2.exe" -WorkingDirectory $gameDir -PassThru
# Written after the launch so the lock always names a real process. With -NoWait the script
# returns while the game runs on, and the lock is what stops the next launch; it goes stale on
# its own the moment that pid dies.
Set-Content $lockPath $proc.Id -Encoding utf8
if ($NoDev) {
    Write-Host "Endless Space 2 started (pid $($proc.Id)). Dev server disabled."
} else {
    Write-Host "Endless Space 2 started (pid $($proc.Id)). Dev server: $devUrl/"
}

# Drive the load through the dev server once the game answers, so one command goes from a cold
# launch to in-game. Done here, before the optional WaitForExit, because that call blocks until
# the game quits. Two waits, both slow on purpose: booting to the main menu takes up to a
# minute (curl retries the connection refusals and the 503s a busy frame answers with), and the
# route itself reports "[not ready]" until the menu can actually start a load.
if ($LoadSave) {
    Write-Host "Waiting for the dev server, then loading '$LoadSave'..."
    $status = curl.exe -s --connect-timeout 5 --retry 120 --retry-connrefused --retry-delay 1 "$devUrl/status"
    if ($status -match '"version"') {
        $loaded = $false
        $answer = ''
        for ($i = 0; $i -lt 60; $i++) {
            # --data-raw, not --data-binary: a title beginning with @ would otherwise be read as
            # the name of a file to send.
            $answer = curl.exe -s -X POST --data-raw "$LoadSave" "$devUrl/loadsave"
            if ($answer -match '"result"\s*:\s*"loaded') { $loaded = $true; break }
            if ($answer -notmatch '\[not ready\]') { break }
            Start-Sleep -Seconds 1
        }
        if ($loaded) {
            Write-Host $answer -ForegroundColor Green
        } else {
            Write-Warning "loading '$LoadSave' did not happen; last answer: $answer"
        }
    } else {
        Write-Warning "the dev server never answered; skipping the load of '$LoadSave'."
    }
}

if (-not $NoWait) {
    $proc.WaitForExit()
    Remove-Item $lockPath -Force -ErrorAction SilentlyContinue
    Write-Host "Game exited with code $($proc.ExitCode)."
}
