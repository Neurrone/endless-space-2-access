#!/bin/sh
# macOS twin of build_release.ps1: builds the player-facing zip for the Mac.
#
#   ./build_release.sh
#
# The zip is not a game-folder overlay like the Windows one - a Mac install needs the
# quarantine flag cleared, the launcher made executable and a Steam launch option printed, so
# the zip unpacks anywhere and macos/install.sh (staged into its root) does the placing. The
# BepInEx macOS runtime and the Prism library are fetched from their pinned releases into
# gitignored caches (bepinex-build/, prism-build/) rather than committed; only a version bump
# or a deleted cache re-downloads. The staging folder is built fresh on every run, so the zip
# can only ever hold what this run produced.
set -eu

root=$(cd "$(dirname "$0")" && pwd)
project="$root/ES2Access/ES2Access.csproj"

version=$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$project" | head -1)
prism_version=$(sed -n 's/.*<PrismVersion>\(.*\)<\/PrismVersion>.*/\1/p' "$project" | head -1)
[ -n "$version" ] || { echo "Could not read Version from $project" >&2; exit 1; }
[ -n "$prism_version" ] || { echo "Could not read PrismVersion from $project" >&2; exit 1; }
# The same BepInEx the committed Windows skeleton in release-template/ carries.
bepinex_version=5.4.23.5

echo "Building v$version (Prism $prism_version, BepInEx $bepinex_version)"
dotnet build "$project" -c Release -v:minimal

# ---- the pinned downloads, cached ----

bepinex_zip="$root/bepinex-build/download/v$bepinex_version-BepInEx_macos_universal_$bepinex_version.zip"
if [ ! -f "$bepinex_zip" ]; then
    mkdir -p "$root/bepinex-build/download"
    curl -fsSL -o "$bepinex_zip" \
        "https://github.com/BepInEx/BepInEx/releases/download/v$bepinex_version/BepInEx_macos_universal_$bepinex_version.zip"
fi

# The csproj's own FetchPrism target does the pinned download (its condition skips it when the
# cache already holds this version), so the fetch exists in exactly one place.
dotnet msbuild "$project" -t:FetchPrism -v:minimal -nologo
prism_dylib="$root/prism-build/libprism.dylib"
[ -f "$prism_dylib" ] || { echo "FetchPrism did not produce $prism_dylib" >&2; exit 1; }

# ---- staging ----

stage="$root/releases/mac-stage"
rm -rf "$stage"
plugin_dir="$stage/BepInEx/plugins/ES2Access"
mkdir -p "$plugin_dir/locale" "$root/releases"

# The BepInEx runtime, except its own launcher: run_bepinex.sh cannot start this game's Mono
# correctly (the header of run-modded.sh says why), and shipping it invites pointing Steam at
# the wrong script.
unzip -o -q "$bepinex_zip" -d "$stage" -x run_bepinex.sh changelog.txt

install -m 755 "$root/macos/run-modded.sh" "$stage/run-modded.sh"
install -m 755 "$root/macos/install.sh" "$stage/install.sh"
cp "$root/macos/README.md" "$stage/README.md"

# libprism.dylib lives beside the .app like prism.dll beside the exe on Windows: the game
# folder is where the loader preloads it from. Licenses renamed on the way in, as the Windows
# zip does, so a bare NOTICE next to the game does not go unattributed.
cp "$prism_dylib" "$stage/libprism.dylib"
cp "$root/vendor/prism/NOTICE" "$stage/prism-NOTICE.txt"
cp "$root/vendor/prism/LICENSE-MPL-2.0.txt" "$stage/prism-LICENSE-MPL-2.0.txt"

cp "$root/ES2Access.Loader/bin/mac/Release/ES2Access.Loader.dll" "$plugin_dir/"
cp "$root/ES2Access/bin/mac/Release/ES2Access.dll" "$plugin_dir/"
# mcs.dll backs POST /eval; the dev server is off unless the config enables it, and no config
# ships, so this only ever loads for someone who turns it on.
cp "$root/vendor/mcs/mcs.dll" "$root/vendor/mcs/NOTICE" "$plugin_dir/"

locales=$(find "$root/ES2Access/locale" -name '*.json' | wc -l)
[ "$locales" -gt 0 ] || { echo "No translation tables in ES2Access/locale" >&2; exit 1; }
cp "$root/ES2Access/locale/"*.json "$plugin_dir/locale/"

# The same refusal the Windows script makes: no debug or config files in a player's zip.
stray=$(find "$stage" \( -name '*.pdb' -o -name '*.cfg' \) | head -5)
if [ -n "$stray" ]; then
    echo "Refusing to package debug or config files:" >&2
    echo "$stray" >&2
    exit 1
fi

zip_path="$root/releases/EndlessSpace2Access-macOS-v$version.zip"
rm -f "$zip_path"
# zip keeps the Unix permission bits, so install.sh and run-modded.sh come out executable.
(cd "$stage" && zip -r -q -X "$zip_path" .)
rm -rf "$stage"
echo "Release zip: $zip_path"
