#!/bin/sh
# walk-all.sh [--reset] <output-dir>
#
# Runs the nine family walks into <output-dir>, one after another. ~18-20 minutes.
# Each family's console output lands in <output-dir>/logs/<family>.log; a family that
# fails is reported and the walk carries on, so one broken screen never costs the run.
#
# --reset first issues `POST /loadsave` with WALK_SAVE (empty = the dev server's newest
# save) and waits for the game to come back. Without it -- the default -- the walk touches
# no save at all and simply walks whatever is loaded. NEVER use --reset between a before
# and an after capture: a load re-instantiates every domain object and reshuffles the
# hash-keyed sheet rows the two dumps are being compared on.
set -u
WALKS_DIR="$(cd "$(dirname "$0")" && pwd)"
. "$WALKS_DIR/fixture.env"

RESET=0
case "${1:-}" in
  --reset) RESET=1; shift ;;
esac
OUTROOT="${1:-}"
[ -n "$OUTROOT" ] || { echo "usage: $0 [--reset] <output-dir>" >&2; exit 2; }
mkdir -p "$OUTROOT/logs" || exit 2
OUTROOT="$(cd "$OUTROOT" && pwd)"
: > "$OUTROOT/skipped.txt"

if ! curl -s -f "$WALK_HOST/status" > "$OUTROOT/status.json"; then
  echo "no dev server at $WALK_HOST -- start the game with the dev gate on" >&2
  exit 1
fi
sed 's/.*"modAssemblyName":"\([^"]*\)".*/build: \1/' "$OUTROOT/status.json" | head -1

if [ "$RESET" -eq 1 ]; then
  echo "resetting: POST /loadsave '${WALK_SAVE}'"
  curl -s -X POST --data-raw "$WALK_SAVE" "$WALK_HOST/loadsave"; echo
  i=0
  while [ "$i" -lt 60 ]; do
    curl -s -X POST --data-raw 'false' "$WALK_HOST/wait?timeout=2000" >/dev/null 2>&1 && break
    i=$((i+1))
  done
fi

START=$(date +%s)
for s in 01-galaxy 02-system 03-empire 04-military 05-diplomacy 06-heroes 07-dialogs 08-notifications 09-bykey; do
  printf '=== %s ' "$s"
  t0=$(date +%s)
  if sh "$WALKS_DIR/$s.sh" "$OUTROOT" > "$OUTROOT/logs/$s.log" 2>&1; then
    printf 'ok (%ss)\n' "$(( $(date +%s) - t0 ))"
  else
    printf 'FAILED (%ss) -- see logs/%s.log\n' "$(( $(date +%s) - t0 ))" "$s"
  fi
done

echo "--- walk complete in $(( $(date +%s) - START ))s"
echo "dumps: $(find "$OUTROOT" -name '*.txt' -not -path '*/.tmp/*' -not -path '*/logs/*' -not -name 'ghosts.txt' -not -name 'routelog.txt' -not -name 'index.txt' -not -name 'skipped.txt' | wc -l | tr -d ' ')"
echo "tooltip captures: $(find "$OUTROOT/tooltips" -type f 2>/dev/null | wc -l | tr -d ' ')"
if [ -s "$OUTROOT/skipped.txt" ]; then
  echo "--- skipped (the fixture could not offer these):"
  cat "$OUTROOT/skipped.txt"
else
  echo "--- skipped: nothing"
fi
