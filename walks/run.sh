#!/bin/sh
# run.sh <output-dir> <target>...
#
# Runs walk scenarios into <output-dir>. A target is a scenario file (`senate/laws-modal.sh`)
# or a folder (`senate`), given relative to walks/ or as a path. Each scenario's console
# output lands in <output-dir>/logs/<family>-<scenario>.log; one that fails is reported and
# the run carries on. Every scenario prints its own wall-clock seconds.
#
# Scenarios never close what they opened -- a folder pays for opening its screen once -- so
# this runner drains ONCE at the end (in game: close every modal and screen, camera to the
# galaxy overview; out of game: back to the main menu) and the fixture is left as found.
#
# There is deliberately no default target: walking every screen is `walk-all.sh`, and only it.
set -u
WALKS_DIR="$(cd "$(dirname "$0")" && pwd)"
. "$WALKS_DIR/fixture.env"

OUTROOT="${1:-}"; shift 2>/dev/null
[ -n "$OUTROOT" ] && [ $# -ge 1 ] || { echo "usage: $0 <output-dir> <scenario-file-or-folder>..." >&2; exit 2; }
mkdir -p "$OUTROOT/logs" || exit 2
OUTROOT="$(cd "$OUTROOT" && pwd)"
touch "$OUTROOT/skipped.txt"

if ! curl -s -f "$WALK_HOST/status" > "$OUTROOT/status.json"; then
  echo "no dev server at $WALK_HOST -- start the game with the dev gate on" >&2
  exit 1
fi
sed 's/.*"modAssemblyName":"\([^"]*\)".*/build: \1/' "$OUTROOT/status.json" | head -1

# resolve <target> -- the scenario files a target names, one per line, in name order. A folder
# may hold a library beside its scenarios (main-menu/menulib.sh); those are sourced, never run,
# and are named *lib.sh exactly as the shared one at the root is.
resolve() {
  t="$1"
  [ -e "$t" ] || t="$WALKS_DIR/$1"
  if [ -d "$t" ]; then find "$t" -maxdepth 1 -name '*.sh' ! -name '*lib.sh' | sort
  elif [ -f "$t" ]; then echo "$t"
  else echo "no such scenario or folder: $1" >&2; fi
}

START=$(date +%s)
for target in "$@"; do
  for s in $(resolve "$target"); do
    fam="$(basename "$(dirname "$s")")"; scn="$(basename "$s" .sh)"
    printf '=== %s/%s ' "$fam" "$scn"
    t0=$(date +%s)
    if sh "$s" "$OUTROOT" > "$OUTROOT/logs/$fam-$scn.log" 2>&1; then
      printf 'ok (%ss)\n' "$(( $(date +%s) - t0 ))"
    else
      printf 'FAILED (%ss) -- see logs/%s-%s.log\n' "$(( $(date +%s) - t0 ))" "$fam" "$scn"
    fi
    grep -h 'A-B-A' "$OUTROOT/logs/$fam-$scn.log" 2>/dev/null | sed 's/^/   /'
  done
done

# leave as found
INGAME=$(curl -s -X POST --data-raw 'Gui.PlayerEmpire != null ? "yes" : "no"' "$WALK_HOST/eval?speech=0" | grep -c '"yes"')
if [ "$INGAME" -eq 1 ]; then
  curl -s -X POST --data-binary "@$WALKS_DIR/cs/drain.cs" "$WALK_HOST/eval?speech=0" >/dev/null
  curl -s -X POST --data-binary "@$WALKS_DIR/cs/tut.cs" "$WALK_HOST/eval?speech=0" >/dev/null
  curl -s -X POST --data-binary "@$WALKS_DIR/cs/reset.cs" "$WALK_HOST/eval?speech=0" >/dev/null
  curl -s -X POST --data-binary "@$WALKS_DIR/cs/restore.cs" "$WALK_HOST/eval?speech=0" >/dev/null
else
  curl -s -X POST --data-binary "@$WALKS_DIR/cs/menuhome.cs" "$WALK_HOST/eval?speech=0" >/dev/null
fi

echo "--- run complete in $(( $(date +%s) - START ))s"
if [ -s "$OUTROOT/skipped.txt" ]; then
  echo "--- skipped (the fixture could not offer these):"
  cat "$OUTROOT/skipped.txt"
fi
