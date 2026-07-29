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
if ($NoDev) { $env:ES2ACCESS_NO_DEV = '1' } else { $env:ES2ACCESS_NO_DEV = $null }

$proc = Start-Process "$gameDir\EndlessSpace2.exe" -WorkingDirectory $gameDir -PassThru
Write-Host "Endless Space 2 started (pid $($proc.Id)). Dev server: http://127.0.0.1:8771/"
if (-not $NoWait) {
    $proc.WaitForExit()
    Write-Host "Game exited with code $($proc.ExitCode)."
}
