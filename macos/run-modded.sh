#!/bin/sh
# Launches Endless Space 2 with BepInEx and the accessibility mod on macOS. Point Steam at it:
# right-click the game, Properties, Launch Options:
#
#   "/path/to/Endless Space 2/run-modded.sh" %command%
#
# The build deploys this script next to the .app; it expects BepInEx's macOS build unzipped
# there too (BepInEx/core and libdoorstop.dylib). It replaces BepInEx's own run_bepinex.sh,
# which cannot start this game's Mono correctly, for three reasons this script exists to fix:
#
# - The game ships Mono.Cecil 0.9.6 in its Managed folder, and Mono binds it before BepInEx's
#   0.10 can be offered, which kills HarmonyX ("Method not found: Mono.Cecil.ModuleDefinition
#   .Dispose"). DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE puts BepInEx/core first; doorstop keeps
#   the Managed folder as the fallback.
# - MonoMod needs libMonoPosixHelper.dylib (Mono maps the MonoPosixHelper P/Invoke to that bare
#   name) to change memory protection on this ancient Mono; the game bundles the library but
#   nothing puts it on the dlopen path. Without it every MonoMod native platform fails, and a
#   latent MonoMod bug (BepInEx's preloader XTermFix resets DetourHelper.Native to null, and the
#   detection that then reruns caches nothing on its no-platform path) leaves Harmony patching
#   with a null platform. DYLD_LIBRARY_PATH pointing at the bundled Mono's osx folder makes the
#   detection succeed for good.
# - run_bepinex.sh execs through /usr/bin/arch on Apple Silicon, and macOS strips every DYLD_*
#   variable when a system binary is exec'd, except the ones arch is explicitly told to pass -
#   which does not include DYLD_LIBRARY_PATH. The game is x86_64-only, so Rosetta runs it
#   regardless of arch, and exec-ing the binary straight from this script keeps every variable.
set -eu

a="/$0"; a=${a%/*}; a=${a#/}; a=${a:-.}; game_dir=$(cd "$a" || exit 1; pwd -P)

# Steam's %command% hands over the command it would have run; with no argument (a plain
# ./run-modded.sh) the game's own binary is used.
executable="${1:-}"
[ $# -gt 0 ] && shift
if [ -z "$executable" ]; then
    executable="$game_dir/EndlessSpace2.app"
fi
case "$executable" in
    *.app)
        inner="$(defaults read "$executable/Contents/Info" CFBundleExecutable)"
        executable="$executable/Contents/MacOS/$inner"
    ;;
esac
if [ ! -x "$executable" ]; then
    echo "game binary not found: $executable" >&2
    exit 1
fi

mono_native_dir="$(dirname "$executable")/../Frameworks/MonoEmbedRuntime/osx"

export DOORSTOP_ENABLED=1
export DOORSTOP_TARGET_ASSEMBLY="$game_dir/BepInEx/core/BepInEx.Preloader.dll"
export DOORSTOP_IGNORE_DISABLED_ENV=0
export DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE="$game_dir/BepInEx/core"
export DYLD_INSERT_LIBRARIES="$game_dir/libdoorstop.dylib"
export DYLD_LIBRARY_PATH="$mono_native_dir"

cd "$game_dir"
exec "$executable" "$@"
