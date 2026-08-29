#!/bin/sh
# macOS twin of run-game.ps1: build, then launch Endless Space 2 through BepInEx's run_bepinex.sh.
#
#   ./run-game.sh [--no-build] [--no-speech] [--no-dev] [--no-wait] [--load-save "<save title>"]
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
if [ ! -x "$game_dir/run_bepinex.sh" ]; then
    echo "BepInEx is not installed next to the .app (no $game_dir/run_bepinex.sh); see macos/README.md" >&2
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

# run_bepinex.sh execs the game (through `arch` on Apple Silicon, which execs in turn), so the
# pid below is the game's own.
log="${TMPDIR:-/tmp}/es2access-run-game.out"
( cd "$game_dir" && exec "$game_dir/run_bepinex.sh" "$app" ) > "$log" 2>&1 &
game_pid=$!
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

if [ "$no_wait" = 0 ]; then
    wait "$game_pid" && code=0 || code=$?
    rm -f "$lock"
    echo "Game exited with code $code."
fi
