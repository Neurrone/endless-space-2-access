#!/bin/sh
# macOS twin of wait-game.ps1: block until the game reaches a state.
#
#   ./wait-game.sh <menu|ingame|loading|dialog> [timeout seconds, default 120]
#
# Exit 0 the state was reached, 1 timed out (the current state is printed), 2 the game is gone.
# The state is DevProbe.State(), the same gates the mod's own screens use. The first 20 seconds
# are exempt from the process check, because Steam and Rosetta take a moment to hand over.
set -u
state="${1:-}"
timeout="${2:-120}"
case "$state" in menu|ingame|loading|dialog) ;; *) echo "usage: $0 <menu|ingame|loading|dialog> [timeout]" >&2; exit 1 ;; esac

dev_url='http://127.0.0.1:8771'
started=$(date +%s)
current=''
while :; do
    elapsed=$(( $(date +%s) - started ))
    if [ "$elapsed" -gt 20 ] && ! pgrep -x EndlessSpace2 >/dev/null 2>&1; then
        echo "game process is gone"
        exit 2
    fi
    # ?speech=0 so eval answers on the frame it runs instead of waiting out a settle window.
    raw="$(curl -s --connect-timeout 2 -X POST --data-raw 'ES2Access.Dev.DevProbe.State()' "$dev_url/eval?speech=0" 2>/dev/null || true)"
    current="$(printf '%s' "$raw" | python3 -c 'import json,sys
try:
    a=json.load(sys.stdin); r=a.get("result")
    print(json.loads(r).get("state","") if r else "")
except Exception:
    print("")' 2>/dev/null)"
    if [ "$current" = "$state" ]; then
        echo "$state ready after ${elapsed}s"
        exit 0
    fi
    if [ "$elapsed" -ge "$timeout" ]; then
        echo "timed out after ${elapsed}s waiting for '$state'; the game is '${current:-unreachable (the dev server is not answering)}'"
        exit 1
    fi
    sleep 0.3
done
