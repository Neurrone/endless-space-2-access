#!/bin/sh
# by-key.sh <output-dir> [screen key...]
#
# Every registered mod screen dumped BY KEY (GET /gui/graph?screen=...&buffers=1), or just
# the keys named. Nothing is opened: an INACTIVE screen answers the stable one-line "screen
# inactive: ..." and an open one renders its live window. Both halves are stable text, which
# is how the out-game family and the unreachable modals get a baseline without leaving the
# session. A tool, not a scenario: it belongs to no screen, so it sits at the root.
#
# The key list is not written down anywhere: a bogus key 400s with the whole registry, so
# the walk asks for one and reads the list out of the refusal.
set -u
FAMILY=by-key; . "$(dirname "$0")/lib.sh" "$1"
shift

: > "$OUT/index.txt"
if [ $# -ge 1 ]; then
  KEYS="$*"
else
  curl -s -o "$TMP/keys.out" "$HOST/gui/graph?screen=walks-probe-no-such-screen"
  KEYS=$(sed 's/.*registered screens are: //; s/"}.*//' "$TMP/keys.out" | tr ',' '\n' | sed 's/^ *//; s/ *$//' | grep '^screen\.')
fi
N=$(echo "$KEYS" | wc -w | tr -d ' ')
echo "   discovered: $N screen keys"
if [ "$N" -lt 1 ]; then
  skip "could not read the screen registry out of a bogus-key refusal - by-key walk empty"
  exit 0
fi

for k in $KEYS; do
  curl -s -o "$OUT/$k.txt" "$HOST/gui/graph?screen=$k&buffers=1"
  printf '%-40s %8s bytes  %s\n' "$k" "$(wc -c < "$OUT/$k.txt" | tr -d ' ')" \
    "$(head -c 60 "$OUT/$k.txt" | tr -d '\n')" >> "$OUT/index.txt"
done
wc -l < "$OUT/index.txt"
done_
