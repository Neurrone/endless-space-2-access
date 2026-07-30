<#
.SYNOPSIS
    Block until the game reaches a state, so a test script can say what it is waiting for.

.DESCRIPTION
    Booting Endless Space 2 takes up to a minute, and loading a save takes another twenty
    seconds - during which the dev server answers, and answers about the wrong thing. Sleeping a
    fixed time is the obvious workaround and the wrong one twice over: too short and the test
    talks to a main menu it thinks is a running game, too long and every run pays for the worst
    case. So this asks.

    The state comes from DevProbe.State(), which reads the same gates the mod's own screens use,
    so "ingame" here means exactly what the mod means by it.

    A dead game is reported as such rather than waited out: a crash during boot would otherwise
    cost the whole timeout and then look like a slow boot. The first 20 seconds are exempt,
    because the process genuinely does not exist yet while Steam or the launcher hands over.

.PARAMETER State
    menu | ingame | loading | dialog

.OUTPUTS
    Exit 0 the state was reached, 1 timed out (the current state is printed), 2 the game is gone.
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('menu', 'ingame', 'loading', 'dialog')]
    [string]$State,

    [int]$TimeoutSec = 120
)

$devUrl = 'http://127.0.0.1:8771'
$pollSeconds = 0.3
$startupGraceSeconds = 20

function Get-GameState {
    # ?speech=0 so eval answers on the frame it runs instead of waiting out a settle window that
    # a poll loop would pay for on every iteration.
    $raw = curl.exe -s --connect-timeout 2 -X POST --data-raw 'ES2Access.Dev.DevProbe.State()' "$devUrl/eval?speech=0" 2>$null
    if (-not $raw) { return $null }
    try {
        $answer = $raw | ConvertFrom-Json
        if (-not $answer.result) { return $null }
        return ($answer.result | ConvertFrom-Json).state
    } catch {
        return $null
    }
}

$started = Get-Date
$current = $null

while ($true) {
    $elapsed = ((Get-Date) - $started).TotalSeconds

    if ($elapsed -gt $startupGraceSeconds) {
        if (-not (Get-Process -Name EndlessSpace2 -ErrorAction SilentlyContinue)) {
            Write-Host "game process is gone" -ForegroundColor Red
            exit 2
        }
    }

    $current = Get-GameState
    if ($current -eq $State) {
        Write-Host "$State ready after $([int]$elapsed)s" -ForegroundColor Green
        exit 0
    }

    if ($elapsed -ge $TimeoutSec) {
        $reported = if ($current) { $current } else { 'unreachable (the dev server is not answering)' }
        Write-Host "timed out after $([int]$elapsed)s waiting for '$State'; the game is '$reported'" -ForegroundColor Yellow
        exit 1
    }

    Start-Sleep -Seconds $pollSeconds
}
