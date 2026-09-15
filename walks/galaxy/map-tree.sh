#!/bin/sh
# The map tree: a system expanded (the camera flies in and the map draws an orbital card per
# planet), focused tooltip probes on its deposit, planet and lane rows; then a second system
# expanded and the first revisited, which is both the pooled-row shrink dump and the A-B-A
# rebind check of the orbital planet cards (PlanetLabel_SystemOrbital). A is whichever of the
# map's first two systems has MORE planets, so that B retires some cards.
#
# Nothing here is named in advance: the constellation is the first the tree lists, the
# systems its first two rows, the planet counts are the model's. Three camera flights, no
# more: the counts come from the model rather than from a flight per candidate.
# Navigation is by type-ahead landings, not counted arrow steps: `ui.home` is
# context-relative on a tree, so a counted walk is not replayable.
set -u
. "$(dirname "$0")/../lib.sh" "$@"

ORBCARDS='t = Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemOrbital>(false).PlanetLabelsContainer;'
# The flight has landed on system X when the view is no longer changing, the focused system
# is X, and the orbital label window is drawing a card bound to one of X's planets. The card
# is the thing the planet rows read from, so this is the frame the rows can be read.
# The pooled container keeps retired labels bound to the previous system, so the test is the
# game's own visible-children count (the drawn test the mod's CardFor asks per card), never
# a fixed child index.
W='Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemOrbital>(false)'
landed() { printf '!ES2Access.UI.GalaxyViewLevels.ChangingLevel && ES2Access.UI.GalaxyViewLevels.FocusedSystem != null && ES2Access.UI.GalaxyViewLevels.FocusedSystem.GUID.ToString() == "%s" && %s != null && %s.Shown && %s.PlanetLabelsContainer.GetVisibleChildrenCount() > 0' "$1" "$W" "$W" "$W"; }

ensure_galaxy
inp ui.focusMap
# The map stop's first rows are bookmarks, so "home" is not the constellation. Read the
# first constellation's own name off the tree and land on it by type-ahead instead.
# The first constellation row is the map stop's first row, so its first field is the stop's
# own caption ("Galactic Map"); the constellation's name is its second field.
field2() { grep -E "\[$2" "$1" | sed -n "${3}p" | sed 's/^[ >]*//; s/  *\[[^]]*\]$//' | cut -d, -f2 | sed 's/^ *//'; }
snap "$TMP/stop.txt"
CONST=$(field2 "$TMP/stop.txt" 'galaxy:constellation/[0-9]*\]' 1)
if [ -n "$CONST" ] && findland "$CONST" 3; then inp ui.right; else inp ui.home; fi
frame; frame
snap "$TMP/map.txt"
SYSRE='galaxy:[^]]*/system/[0-9]*\]'
SYSA=$(label_nth "$TMP/map.txt" "$SYSRE" 1); KEYA=$(key_nth "$TMP/map.txt" "$SYSRE" 1)
SYSB=$(label_nth "$TMP/map.txt" "$SYSRE" 2); KEYB=$(key_nth "$TMP/map.txt" "$SYSRE" 2)
if [ -z "$SYSA" ]; then
  skip "no star system in the map tree - the whole map leg is unreachable"; done_; exit 0
fi
# planets <guid> -- how many planets the model gives the system (the count the cards follow)
planets() { fact "((System.Func<string>)(() => { var gm = UnityEngine.Object.FindObjectOfType<GalaxyManager>(); var f = typeof(GalaxyManager).GetField(\"starSystemNodes\", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance); var arr = (StarSystemNode[])f.GetValue(gm); for (int k = 0; k < arr.Length; k++) { if (arr[k].GUID.ToString() == \"$1\") { System.Collections.IList ps = (System.Collections.IList)arr[k].Planets; return (ps == null ? 0 : ps.Count).ToString(); } } return \"0\"; }))()"; }
NA=$(planets "${KEYA##*/}"); NB=0
[ -n "$SYSB" ] && NB=$(planets "${KEYB##*/}")
echo "   discovered: $(nkeys "$TMP/map.txt" "$SYSRE") systems; [$SYSA] has $NA planets, [$SYSB] has $NB"
if [ "${NB:-0}" -gt "${NA:-0}" ]; then OA=$SYSB; OKA=$KEYB; OB=$SYSA; OKB=$KEYA; else OA=$SYSA; OKA=$KEYA; OB=$SYSB; OKB=$KEYB; fi

# flyto <label> <key> -- expand the system's row (the camera flies in) and wait for the
# landing. A row that is already expanded is collapsed first: Right on an expanded row steps
# into it and flies nowhere, which is what left an earlier reading on another system's camera.
flyto() {
  inp ui.focusMap
  findland "$1" || return 1
  snap "$TMP/row.txt"
  if grep -F "[$2]" "$TMP/row.txt" | grep -q ', expanded'; then inp ui.left; fi
  inp ui.right
  waitfor "$(landed "${2##*/}")" 20000 || echo "   NOTE: the camera did not land on $2 with a card drawn"
  frame; frame
}

# ---- system A expanded: the reading, then its probes ----------------------------------
flyto "$OA" "$OKA" || { skip "type-ahead could not land on system A - the map leg is unreachable"; done_; exit 0; }
at system-a-expanded
dump system-a-expanded
ghosts "galaxy, system A expanded"
gwalk graph a "$OKA"
gpool pool  a "$ORBCARDS"

# Probes AFTER the reading: landing on rows elsewhere in the tree pans the camera, and with
# the camera off the system its cards are undrawn and the planet rows fall back to leaves.
# A region's FIRST row carries the region's caption as its first field ("Planets, Leo I"),
# so the planet probe prefers row 2 and a lone first row is read by its second field.
snap "$TMP/expanded.txt"
delay0
# Deposit rows are keyed by the deposit's NAME (deposit/StrategicDeposit1), planet and lane
# rows by number, so the key pattern is per kind.
for pair in "deposit|[^]]*|system-deposit" "planet|[0-9]*|planet-card" "lane|[0-9]*|starlane"; do
  kind=$(echo "$pair" | cut -d'|' -f1); sub=$(echo "$pair" | cut -d'|' -f2); lbl=$(echo "$pair" | cut -d'|' -f3)
  txt=$(label_nth "$TMP/expanded.txt" "$OKA/$kind/$sub\]" 2)
  [ -n "$txt" ] || txt=$(field2 "$TMP/expanded.txt" "$OKA/$kind/$sub\]" 1)
  if [ -n "$txt" ] && findland "$txt"; then at "tip-$lbl"; tip "$lbl"
  else skip "system A has no $kind row - $lbl tooltip not captured"; fi
done
delayrestore

# ---- system B expanded, then A revisited (the pool shrink and the A-B-A) ---------------
if [ -z "$OB" ]; then
  skip "fewer than two systems in the map tree - second-system dump and orbital rebind unreachable"
elif ! flyto "$OB" "$OKB"; then
  skip "type-ahead could not land on system B - second-system dump and orbital rebind unreachable"
else
  at system-b-expanded
  dump system-b-expanded
  ghosts "galaxy, system B expanded"
  gpool poolb b "$ORBCARDS"

  flyto "$OA" "$OKA"
  at system-a-revisited
  dump system-a-revisited
  ghosts "galaxy, system A revisited (pool shrink)"
  gwalk graph a2 "$OKA"
  gpool pool  a2 "$ORBCARDS"
  aba graph pool
fi
done_
