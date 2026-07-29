param(
    # Assemblies from the game's Managed folder to decompile into decompiled\<name>\
    [string[]]$Assemblies = @('Assembly-CSharp', 'Assembly-CSharp-firstpass', 'Amplitude')
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

if (-not (Get-Command ilspycmd -ErrorAction SilentlyContinue)) {
    Write-Error "ilspycmd not found. Install with: dotnet tool install -g ilspycmd"
}

$props = [xml](Get-Content "$root\GamePaths.props")
$pg = $props.Project.PropertyGroup | Where-Object { $_.GameDir } | Select-Object -First 1
$managed = $pg.Managed.Replace('$(GameDir)', $pg.GameDir)
if (-not (Test-Path $managed)) {
    Write-Error "Managed folder not found: $managed (check GamePaths.props)"
}

foreach ($asm in $Assemblies) {
    $dll = Join-Path $managed "$asm.dll"
    if (-not (Test-Path $dll)) {
        Write-Error "Assembly not found: $dll"
    }
    $out = Join-Path "$root\decompiled" $asm
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    New-Item -ItemType Directory -Force $out | Out-Null
    Write-Host "Decompiling $asm.dll ..."
    # cmd /c so ilspycmd's stderr chatter (e.g. update nags) can't become a PowerShell error
    cmd /c "ilspycmd -p `"$dll`" -o `"$out`" 2>nul"
    if ($LASTEXITCODE -ne 0) { Write-Error "ilspycmd failed for $asm (exit $LASTEXITCODE)" }
    $count = (Get-ChildItem $out -Recurse -Filter *.cs).Count
    Write-Host "  $count files -> decompiled\$asm"
}
