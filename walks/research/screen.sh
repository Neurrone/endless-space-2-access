#!/bin/sh
# The technology wheel: its dump, and a focused tooltip probe on row 2 of the suggested
# technologies (row 1 carries the region's caption).
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ensure_screen screen.research TechnologyScreen
capture wheel "technology wheel"
snap "$TMP/res.txt"
SUGRE='research:suggested/'
NSUG=$(nkeys "$TMP/res.txt" "$SUGRE")
echo "   discovered: $NSUG suggested-technology row(s)"
delay0
# Rows start at 2 per the caption rule. Which of them the probe uses is not fixed either: the
# type-ahead drops a hyphen out of the query but not out of the text it matches against, so a
# technology whose name holds one ("High-Energy Magnetics") can never be searched for. The
# route therefore takes the first row it can actually land on.
n=2; TECH=""
while [ "$n" -le "${NSUG:-0}" ]; do
  t=$(label_nth "$TMP/res.txt" "$SUGRE" "$n")
  # The suggested band is the stop the reset lands beside, so a landing that needs more than
  # two stops is a name type-ahead cannot match, not a row elsewhere: give up early.
  if [ -n "$t" ] && findland "$t" 2; then TECH=$t; break; fi
  n=$((n+1))
done
if [ -n "$TECH" ]; then
  echo "   discovered: technology [$TECH]"
  at tip-technology; tip technology
else
  skip "no suggested-technology row past the first could be reached by type-ahead - technology tooltip not captured"
fi
delayrestore
done_
