#!/bin/sh
# walk-all.sh [--reset] <output-dir>
#
# THE FULL WALK: every in-game scenario folder, in order, through run.sh. This is the only
# entry point that walks everything, and CLAUDE.md gates running it on the owner's explicit
# approval; a change to one screen is proved with `run.sh <dir> <folder or file>` instead.
#
# --reset first issues `POST /loadsave` with WALK_SAVE (empty = the dev server's newest
# save) and waits for the game to come back. Without it -- the default -- the walk touches
# no save at all and simply walks whatever is loaded. NEVER use --reset between a before
# and an after capture: a load re-instantiates every domain object and reshuffles the
# hash-keyed sheet rows the two dumps are being compared on.
#
# main-menu/ is not here: it walks what the game draws BEFORE a save is loaded and is run
# on its own against a freshly launched game sitting at the main menu.
set -u
WALKS_DIR="$(cd "$(dirname "$0")" && pwd)"
. "$WALKS_DIR/fixture.env"

RESET=0
case "${1:-}" in
  --reset) RESET=1; shift ;;
esac
OUTROOT="${1:-}"
[ -n "$OUTROOT" ] || { echo "usage: $0 [--reset] <output-dir>" >&2; exit 2; }

if [ "$RESET" -eq 1 ]; then
  echo "resetting: POST /loadsave '${WALK_SAVE}'"
  curl -s -X POST --data-raw "$WALK_SAVE" "$WALK_HOST/loadsave"; echo
  i=0
  while [ "$i" -lt 60 ]; do
    curl -s -X POST --data-raw 'false' "$WALK_HOST/wait?timeout=2000" >/dev/null 2>&1 && break
    i=$((i+1))
  done
fi

sh "$WALKS_DIR/run.sh" "$OUTROOT" \
  galaxy system-management research quests empire economy senate military diplomacy heroes \
  notifications game-menu end-game dialogs \
  "$WALKS_DIR/by-key.sh"

echo "dumps: $(find "$OUTROOT" -name '*.txt' -not -path '*/.tmp/*' -not -path '*/logs/*' -not -name 'ghosts.txt' -not -name 'routelog.txt' -not -name 'index.txt' -not -name 'skipped.txt' | wc -l | tr -d ' ')"
echo "tooltip captures: $(find "$OUTROOT/tooltips" -type f 2>/dev/null | wc -l | tr -d ' ')"
