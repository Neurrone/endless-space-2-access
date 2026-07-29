param(
    [switch]$NoBuild,
    [switch]$NoSpeech,
    [switch]$NoDev,
    [switch]$NoWait,
    # By default a watcher hands focus back to the window you were using whenever the game
    # grabs it during boot; pass -Foreground to let the game take and keep focus.
    [switch]$Foreground
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

Add-Type -Namespace Win32 -Name Focus -MemberDefinition @'
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
'@

$prevWindow = [Win32.Focus]::GetForegroundWindow()
$proc = Start-Process "$gameDir\EndlessSpace2.exe" -WorkingDirectory $gameDir -PassThru

if (-not $Foreground) {
    # Unity ignores the no-activate startup hint, so a detached watcher restores focus to the
    # previous window each time the game grabs it while booting. The game stays visible (so
    # /screenshot keeps rendering) and keeps simulating via Application.runInBackground.
    $watcher = @'
Add-Type -Namespace Win32 -Name Focus -MemberDefinition @"
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
[DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
"@
$prev = [IntPtr]__PREV__
$deadline = (Get-Date).AddSeconds(90)
while ((Get-Date) -lt $deadline) {
    $fg = [Win32.Focus]::GetForegroundWindow()
    $fgPid = [uint32]0
    $fgThread = [Win32.Focus]::GetWindowThreadProcessId($fg, [ref]$fgPid)
    if ($fgPid -eq __GAMEPID__ -and $prev -ne [IntPtr]::Zero) {
        $me = [Win32.Focus]::GetCurrentThreadId()
        [Win32.Focus]::AttachThreadInput($me, $fgThread, $true) | Out-Null
        [Win32.Focus]::SetForegroundWindow($prev) | Out-Null
        [Win32.Focus]::AttachThreadInput($me, $fgThread, $false) | Out-Null
    }
    Start-Sleep -Milliseconds 400
}
'@
    $watcher = $watcher.Replace('__PREV__', $prevWindow.ToInt64()).Replace('__GAMEPID__', $proc.Id)
    $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($watcher))
    Start-Process powershell -WindowStyle Hidden -ArgumentList @(
        '-NoProfile', '-EncodedCommand', $encoded
    ) | Out-Null
}

if ($NoDev) {
    Write-Host "Endless Space 2 started (pid $($proc.Id)). Dev server disabled."
} else {
    Write-Host "Endless Space 2 started (pid $($proc.Id)). Dev server: http://127.0.0.1:8771/"
}
if (-not $NoWait) {
    $proc.WaitForExit()
    Write-Host "Game exited with code $($proc.ExitCode)."
}
