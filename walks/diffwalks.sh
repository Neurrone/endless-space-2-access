#!/bin/sh
# diffwalks.sh <dirA> <dirB> [report-file]
#
# Diffs two walk outputs with the volatile classes normalised (normalize.sed). Prints
# `total differing lines: N`; N must be 0 for "this change altered no spoken or buffer
# line". Anything else is a real change to classify.
#
# Diagnostics are deliberately NOT diffed -- ghosts.txt, routelog.txt, index.txt,
# skipped.txt, status.json, logs/ and .tmp/. They are how you EXPLAIN a diff, not part of
# the regression surface. Read routelog.txt first when a diff looks noisy: a route that
# landed somewhere else explains everything.
set -u
A="${1:?usage: diffwalks.sh <dirA> <dirB> [report]}"
B="${2:?usage: diffwalks.sh <dirA> <dirB> [report]}"
OUTF="${3:-diffwalks.txt}"
SEDF="$(cd "$(dirname "$0")" && pwd)/normalize.sed"
: > "$OUTF"

listfiles() (
  cd "$1" || exit 1
  find . -type f \
    ! -name 'ghosts.txt' ! -name 'routelog.txt' ! -name 'index.txt' \
    ! -name 'skipped.txt' ! -name 'status.json' \
    ! -path './logs/*' ! -path './.tmp/*' | sort
)

TA="$OUTF.a.tmp"; TB="$OUTF.b.tmp"
tot=0
for f in $(listfiles "$A"); do
  if [ ! -f "$B/$f" ]; then
    echo "MISSING IN B: $f" >> "$OUTF"; tot=$((tot+1)); continue
  fi
  sed -f "$SEDF" "$A/$f" > "$TA"
  sed -f "$SEDF" "$B/$f" > "$TB"
  n=$(diff "$TA" "$TB" | grep -c '^[<>]')
  if [ "$n" -gt 0 ]; then
    printf '\n===== %s : %s differing lines =====\n' "$f" "$n" >> "$OUTF"
    diff "$TA" "$TB" | cut -c1-300 >> "$OUTF"
    tot=$((tot+n))
  fi
done
for f in $(listfiles "$B"); do
  [ -f "$A/$f" ] || { echo "MISSING IN A: $f" >> "$OUTF"; tot=$((tot+1)); }
done
rm -f "$TA" "$TB"

if [ -f "$A/skipped.txt" ] || [ -f "$B/skipped.txt" ]; then
  if ! diff "$A/skipped.txt" "$B/skipped.txt" >/dev/null 2>&1; then
    echo "NOTE: the two walks skipped different captures -- compare skipped.txt before reading the diff"
  fi
fi
echo "total differing lines: $tot"
echo "report: $OUTF"
