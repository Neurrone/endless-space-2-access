Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Builds the player-facing zip: the committed BepInEx 5 runtime skeleton in release-template,
# plus the freshly built mod payload staged into it. Staging happens here and not in an MSBuild
# target so that an ordinary build (which deploys to the game folder) can never leave a stale or
# machine-specific file inside the template.

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptDir "ES2Access\ES2Access.csproj"
$loaderDir = Join-Path $scriptDir "ES2Access.Loader\bin\Release"
$modDir = Join-Path $scriptDir "ES2Access\bin\Release"
$vendorDir = Join-Path $scriptDir "vendor"
$localeDir = Join-Path $scriptDir "ES2Access\locale"
$descriptionsDir = Join-Path $scriptDir "ES2Access\descriptions"
$templateDir = Join-Path $scriptDir "release-template"
$releaseDir = Join-Path $scriptDir "releases"

[xml]$project = Get-Content -LiteralPath $projectPath
$version = $project.Project.PropertyGroup |
    ForEach-Object { $_.Version } |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($version)) {
    throw "Could not read Version from $projectPath"
}

if (-not (Test-Path -LiteralPath $templateDir)) {
    throw "Release template not found: $templateDir"
}

# Everything this script writes into the template. Cleared before staging so the zip can only
# ever contain what this run produced; the rest of the template is the committed skeleton.
$stagedRoots = @(
    (Join-Path $templateDir "prism.dll"),
    (Join-Path $templateDir "prism-NOTICE.txt"),
    (Join-Path $templateDir "prism-LICENSE-MPL-2.0.txt"),
    (Join-Path $templateDir "BepInEx\plugins")
)

$pluginDir = Join-Path $templateDir "BepInEx\plugins\ES2Access"
$zipPath = Join-Path $releaseDir "EndlessSpace2Access-v$version.zip"

Push-Location $scriptDir
try {
    dotnet build $projectPath -c Release -v:minimal
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE"
    }

    foreach ($staged in $stagedRoots) {
        if (Test-Path -LiteralPath $staged) {
            Remove-Item -LiteralPath $staged -Recurse -Force
        }
    }

    New-Item -ItemType Directory -Force (Join-Path $pluginDir "locale") | Out-Null
    New-Item -ItemType Directory -Force (Join-Path $pluginDir "descriptions") | Out-Null

    # prism.dll lives beside the game executable, not under BepInEx: it is a native library the
    # mod loads by name, so the game root is the only folder the loader will find it in.
    Copy-Item -LiteralPath (Join-Path $vendorDir "prism\prism.dll") -Destination $templateDir
    # Renamed on the way in: a bare NOTICE beside the game exe would not say whose it is.
    Copy-Item -LiteralPath (Join-Path $vendorDir "prism\NOTICE") `
        -Destination (Join-Path $templateDir "prism-NOTICE.txt")
    Copy-Item -LiteralPath (Join-Path $vendorDir "prism\LICENSE-MPL-2.0.txt") `
        -Destination (Join-Path $templateDir "prism-LICENSE-MPL-2.0.txt")

    Copy-Item -LiteralPath (Join-Path $loaderDir "ES2Access.Loader.dll") -Destination $pluginDir
    Copy-Item -LiteralPath (Join-Path $modDir "ES2Access.dll") -Destination $pluginDir
    # mcs.dll backs POST /eval; the dev server is off unless the config enables it, and the
    # config is not shipped, so this only ever loads for someone who turns it on.
    Copy-Item -LiteralPath (Join-Path $vendorDir "mcs\mcs.dll") -Destination $pluginDir
    Copy-Item -LiteralPath (Join-Path $vendorDir "mcs\NOTICE") -Destination $pluginDir

    $localeFiles = @(Get-ChildItem -LiteralPath $localeDir -Filter *.json -File)
    if ($localeFiles.Count -eq 0) {
        throw "No translation tables found in $localeDir"
    }
    Copy-Item -LiteralPath $localeFiles.FullName -Destination (Join-Path $pluginDir "locale")

    # Cutscene audio descriptions. A hard failure rather than a warning: unlike a translation
    # there is nothing to fall back to, so a release missing these ships a feature that silently
    # says nothing.
    $descriptionFiles = @(Get-ChildItem -LiteralPath $descriptionsDir -Filter *.json -File)
    if ($descriptionFiles.Count -eq 0) {
        throw "No description tables found in $descriptionsDir"
    }
    Copy-Item -LiteralPath $descriptionFiles.FullName -Destination (Join-Path $pluginDir "descriptions")

    $stray = @(Get-ChildItem -LiteralPath $templateDir -Recurse -File -Force |
        Where-Object { $_.Extension -in @(".pdb", ".cfg") })
    if ($stray.Count -gt 0) {
        throw "Refusing to package debug or config files: $(($stray | ForEach-Object { $_.Name }) -join ', ')"
    }

    New-Item -ItemType Directory -Force $releaseDir | Out-Null
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    # The template's CONTENTS, not the folder: the player extracts into the game folder.
    Compress-Archive -Path (Join-Path $templateDir "*") -DestinationPath $zipPath -Force

    Write-Host "Release zip: $zipPath"
}
finally {
    Pop-Location
}
