#!/bin/sh
# Installs Endless Space 2 Access next to the game's .app on macOS. Run it from the unzipped
# release folder, in Terminal:
#
#   ./install.sh
#
# or, if the game is not in Steam's usual place, pass the game folder (the one that CONTAINS
# EndlessSpace2.app):
#
#   ./install.sh "/path/to/Endless Space 2"
#
# It copies the BepInEx runtime, the mod, the launcher and the Prism fallback library next to
# the .app, clears macOS's quarantine flag (a downloaded library is refused without that), and
# prints the launch option to paste into Steam. Running it again over an existing install is
# fine: it overwrites the mod with this version and touches nothing else.
set -eu

here=$(cd "$(dirname "$0")" && pwd)
game_dir="${1:-$HOME/Library/Application Support/Steam/steamapps/common/Endless Space 2}"

if [ ! -d "$game_dir/EndlessSpace2.app" ]; then
    echo "Endless Space 2 was not found at: $game_dir" >&2
    echo "Pass the game folder (the one containing EndlessSpace2.app) as the argument:" >&2
    echo "  ./install.sh \"/path/to/Endless Space 2\"" >&2
    exit 1
fi

if [ ! -d "/Library/Frameworks/Mono.framework" ]; then
    echo "The game's Mono framework is not installed yet. Launch Endless Space 2 once from"
    echo "Steam first: the game installs Mono on that launch and quits. Launch it again, quit"
    echo "from the menu, then run this installer."
    exit 1
fi

echo "Installing into: $game_dir"
mkdir -p "$game_dir/BepInEx"
cp -R "$here/BepInEx/." "$game_dir/BepInEx/"
for file in libdoorstop.dylib .doorstop_version libprism.dylib prism-NOTICE.txt prism-LICENSE-MPL-2.0.txt; do
    cp "$here/$file" "$game_dir/"
done
install -m 755 "$here/run-modded.sh" "$game_dir/run-modded.sh"

# A file that arrived in a browser download carries the quarantine flag, and cp keeps it;
# cleared here so the game may load the libraries. Harmless where nothing is flagged.
xattr -dr com.apple.quarantine "$game_dir" 2>/dev/null || true

echo ""
echo "Installed. One step remains, in Steam:"
echo "right-click Endless Space 2, Properties, General, Launch Options, and paste this line"
echo "(including the quotes):"
echo ""
echo "\"$game_dir/run-modded.sh\" %command%"
echo ""
echo "Then launch from Steam. The mod announces itself once the main menu is up. The voice and"
echo "speaking rate are your Spoken Content settings; the Speech tab under Mod settings (right"
echo "after Options in the game's main menu) changes them, and README.md in this folder has"
echo "the details and the key list."
