#!/bin/sh
# macOS twin of run-game.ps1: build, then launch Endless Space 2 through macos/run-modded.sh,
# as a launchd job in the GUI session.
#
#   ./run-game.sh [--no-build] [--no-speech] [--no-dev] [--no-wait] [--load-save "<save title>"]
#
# The GUI session matters for speech: a process started from an SSH login cannot reach the
# Eloquence voices, and AVSpeech silently swaps in the compact default voice - the mod would ask
# for the player's Spoken Content voice and something else would answer. launchd's gui domain
# runs the game in the logged-in desktop session whichever terminal this script runs from,
# exactly as a Steam launch would.
#
# The game folder comes from GamePaths.props (GameDir). Steam must be running: the game's own
# steam_api needs it, whichever way the binary is started. One game at a time, as on Windows:
# a second copy would find the dev port taken and answer nothing, and every request would then
# reach the first game. A running game is never killed here.
set -eu

root="$(cd "$(dirname "$0")" && pwd)"
dev_url='http://127.0.0.1:8771'
dev_port=8771
lock="${TMPDIR:-/tmp}/es2access-run-game.lock"

no_build=0; no_speech=0; no_dev=0; no_wait=0; load_save=''
while [ $# -gt 0 ]; do
    case "$1" in
        --no-build) no_build=1 ;;
        --no-speech) no_speech=1 ;;
        --no-dev) no_dev=1 ;;
        --no-wait) no_wait=1 ;;
        --load-save) shift; load_save="${1:-}" ;;
        *) echo "unknown option: $1" >&2; exit 1 ;;
    esac
    shift
done
if [ -n "$load_save" ] && [ "$no_dev" = 1 ]; then
    echo "--load-save drives the dev server's POST /loadsave, so it cannot be used with --no-dev." >&2
    exit 1
fi

# The props file may hold a Windows GameDir and a macOS one (the template's OS-conditioned
# blocks); the macOS value is the one that names an absolute POSIX path. MSBuild picks by the
# blocks' own conditions, so this only has to agree with it, not evaluate it.
game_dir="$(sed -n 's/.*<GameDir>\(.*\)<\/GameDir>.*/\1/p' "$root/GamePaths.props" \
    | sed "s|\$(HOME)|$HOME|" | grep '^/' | head -1 || true)"
if [ -z "$game_dir" ]; then
    echo "no macOS GameDir in GamePaths.props (the macOS value is an absolute path; see GamePaths.props.template)" >&2
    exit 1
fi
app="$game_dir/EndlessSpace2.app"
if [ ! -d "$app" ]; then
    echo "game not found: $app (check GameDir in GamePaths.props)" >&2
    exit 1
fi
if [ ! -f "$game_dir/BepInEx/core/BepInEx.Preloader.dll" ] || [ ! -f "$game_dir/libdoorstop.dylib" ]; then
    echo "BepInEx is not installed next to the .app (no BepInEx/core or libdoorstop.dylib in $game_dir); see macos/README.md" >&2
    exit 1
fi

game_running() { pgrep -x EndlessSpace2 >/dev/null 2>&1; }
port_bound() { lsof -nP -iTCP:"$dev_port" -sTCP:LISTEN >/dev/null 2>&1; }

if [ -f "$lock" ]; then
    locked_pid="$(tr -d '[:space:]' < "$lock" 2>/dev/null || true)"
    if [ -n "$locked_pid" ] && [ "$(ps -p "$locked_pid" -o comm= 2>/dev/null)" = "EndlessSpace2" ]; then
        echo "Endless Space 2 is already running as pid $locked_pid (lock: $lock). Quit it first - POST $dev_url/quit - or delete the lock if that pid is not the game." >&2
        exit 1
    fi
    rm -f "$lock"
fi

# A game on its way out holds the port for a few seconds after POST /quit answers.
waited=0
while game_running || port_bound; do
    if [ "$waited" -ge 15 ]; then
        echo "Endless Space 2 has not finished shutting down after 15s. Nothing was launched and nothing was killed - quit the running game yourself, then retry." >&2
        exit 1
    fi
    echo "Waiting for the previous game to exit..."
    sleep 1
    waited=$((waited + 1))
done

if [ "$no_build" = 0 ]; then
    dotnet build "$root/ES2Access/ES2Access.csproj"
fi

if [ "$no_speech" = 1 ]; then export ES2ACCESS_NO_SPEECH=1; else unset ES2ACCESS_NO_SPEECH; fi

# The dev server is opt-in via BepInEx config (off for players); dev runs opt in here.
cfg="$game_dir/BepInEx/config/endless.space2.access.cfg"
dev_value=true; [ "$no_dev" = 1 ] && dev_value=false
mkdir -p "$(dirname "$cfg")"
if [ -f "$cfg" ] && grep -q '^[[:space:]]*devServer[[:space:]]*=' "$cfg"; then
    sed -i '' "s/^[[:space:]]*devServer[[:space:]]*=.*/devServer = $dev_value/" "$cfg"
elif [ -f "$cfg" ] && grep -q '^\[Dev\]' "$cfg"; then
    sed -i '' "s/^\[Dev\][[:space:]]*$/[Dev]\\
devServer = $dev_value/" "$cfg"
else
    printf '\n[Dev]\ndevServer = %s\n' "$dev_value" >> "$cfg"
fi

launcher="$game_dir/run-modded.sh"
if [ ! -x "$launcher" ]; then
    echo "the launcher is not deployed at $launcher; build first (dotnet build ES2Access/ES2Access.csproj)" >&2
    exit 1
fi

# The game's steam_api asks Steam to relaunch the game when started outside Steam, unless the
# app id sits next to the binary.
[ -f "$game_dir/steam_appid.txt" ] || printf '392110\n' > "$game_dir/steam_appid.txt"

log="${TMPDIR:-/tmp}/es2access-run-game.out"
label="es2access.game"
plist="${TMPDIR:-/tmp}/es2access-game.plist"
uid="$(id -u)"
launchctl bootout "gui/$uid/$label" 2>/dev/null || true
speech_env=""
if [ "$no_speech" = 1 ]; then
    speech_env="  <key>EnvironmentVariables</key><dict><key>ES2ACCESS_NO_SPEECH</key><string>1</string></dict>"
fi
cat > "$plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>Label</key><string>$label</string>
  <key>ProgramArguments</key><array><string>$launcher</string></array>
  <key>WorkingDirectory</key><string>$game_dir</string>
  <key>StandardOutPath</key><string>$log</string>
  <key>StandardErrorPath</key><string>$log</string>
  <key>RunAtLoad</key><true/>
$speech_env
</dict>
</plist>
EOF
if ! launchctl bootstrap "gui/$uid" "$plist"; then
    echo "warning: no GUI session to launch into; starting directly (speech will not use the Spoken Content voice)" >&2
    ( cd "$game_dir" && exec "$launcher" ) > "$log" 2>&1 &
fi

game_pid=""
waited=0
while [ -z "$game_pid" ] && [ "$waited" -lt 30 ]; do
    game_pid="$(pgrep -x EndlessSpace2 || true)"
    [ -n "$game_pid" ] && break
    sleep 1
    waited=$((waited + 1))
done
if [ -z "$game_pid" ]; then
    echo "the game did not start; launcher output: $log" >&2
    tail -5 "$log" >&2 || true
    launchctl bootout "gui/$uid/$label" 2>/dev/null || true
    exit 1
fi
printf '%s\n' "$game_pid" > "$lock"
if [ "$no_dev" = 1 ]; then
    echo "Endless Space 2 started (pid $game_pid). Dev server disabled. Launcher output: $log"
else
    echo "Endless Space 2 started (pid $game_pid). Dev server: $dev_url/  Launcher output: $log"
fi

if [ -n "$load_save" ]; then
    echo "Waiting for the dev server, then loading '$load_save'..."
    status="$(curl -s --connect-timeout 5 --retry 120 --retry-connrefused --retry-delay 1 "$dev_url/status" || true)"
    case "$status" in
        *'"version"'*)
            loaded=0; answer=''
            i=0
            while [ $i -lt 60 ]; do
                answer="$(curl -s -X POST --data-raw "$load_save" "$dev_url/loadsave" || true)"
                case "$answer" in
                    *'"result"'*'"loaded'*) loaded=1; break ;;
                    *'[not ready]'*) sleep 1 ;;
                    *) break ;;
                esac
                i=$((i + 1))
            done
            if [ "$loaded" = 1 ]; then echo "$answer"; else echo "warning: loading '$load_save' did not happen; last answer: $answer" >&2; fi
            ;;
        *) echo "warning: the dev server never answered; skipping the load of '$load_save'." >&2 ;;
    esac
fi

# The game is launchd's child, not ours, so waiting is polling.
if [ "$no_wait" = 0 ]; then
    while kill -0 "$game_pid" 2>/dev/null; do
        sleep 2
    done
    launchctl bootout "gui/$uid/$label" 2>/dev/null || true
    rm -f "$lock"
    echo "Game exited."
fi
