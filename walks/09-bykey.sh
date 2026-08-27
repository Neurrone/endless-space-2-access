#!/bin/sh
# Every registered mod screen dumped BY KEY (GET /gui/graph?screen=...&buffers=1).
# This is how the out-game family and the unreachable modals get a baseline without
# leaving the session: an INACTIVE screen answers the stable one-line "screen inactive:
# ..." or renders its live (possibly stale) window. Both halves are stable text.
#
# The key list is not written down anywhere: a bogus key 400s with the whole registry,
# so the walk asks for one and reads the list out of the refusal.
set -u
FAMILY=bykey; . "$(dirname "$0")/lib.sh" "$@"

prologue
: > "$OUT/index.txt"

curl -s -o "$TMP/keys.out" "$HOST/gui/graph?screen=walks-probe-no-such-screen"
KEYS=$(sed 's/.*registered screens are: //; s/"}.*//' "$TMP/keys.out" | tr ',' '\n' | sed 's/^ *//; s/ *$//' | grep '^screen\.')
N=$(echo "$KEYS" | grep -c .)
echo "   discovered: $N registered screen keys"
if [ "$N" -lt 2 ]; then
  skip "could not read the screen registry out of a bogus-key refusal - by-key walk empty"
  epilogue
  exit 0
fi

for k in $KEYS; do
  curl -s -o "$OUT/$k.txt" "$HOST/gui/graph?screen=$k&buffers=1"
  printf '%-40s %8s bytes  %s\n' "$k" "$(wc -c < "$OUT/$k.txt" | tr -d ' ')" \
    "$(head -c 60 "$OUT/$k.txt" | tr -d '\n')" >> "$OUT/index.txt"
done
wc -l < "$OUT/index.txt"
epilogue
