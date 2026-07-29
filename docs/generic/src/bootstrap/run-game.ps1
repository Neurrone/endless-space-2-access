param(
    [switch]$NoBuild,
    [switch]$NoSpeech,
    [switch]$NoDev,
    [switch]$NoWait
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

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
if ($NoDev) {
    Write-Host "Endless Space 2 started (pid $($proc.Id)). Dev server disabled."
} else {
    Write-Host "Endless Space 2 started (pid $($proc.Id)). Dev server: http://127.0.0.1:8771/"
}
if (-not $NoWait) {
    $proc.WaitForExit()
    Write-Host "Game exited with code $($proc.ExitCode)."
}
