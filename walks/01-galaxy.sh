#!/bin/sh
# Family: galaxy HUD, the map tree (two systems expanded and one revisited), the
# selected-fleet panel, scan view, and the focused-tooltip pass over the map's
# Class-backed carriers.
#
# Nothing here is named in advance. The two systems are the first two rows of the map
# tree; the planet and the starlane are the first children of the first system; the HUD
# targets are addressed by node key and their labels read back from the dump. The fleet
# is the first one the empire owns that type-ahead can actually reach.
#
# Navigation is by TYPE-AHEAD landings, not counted arrow steps: `ui.home` is
# context-relative on a tree, so a counted walk is not replayable.
set -u
FAMILY=galaxy; . "$(dirname "$0")/lib.sh" "$@"

prologue
inp ui.focusMap
inp ui.home          # the constellation row, collapsed
inp ui.right         # expand it and step in -> the first system
pause 1500

at prologue
dump 01-overview
ghosts "galaxy overview"

# ---- discover the map's own contents -------------------------------------------------
snap "$TMP/map.txt"
SYSRE='galaxy:[^]]*/system/[0-9]*\]'
NSYS=$(nkeys "$TMP/map.txt" "$SYSRE")
SYSA=$(label_nth "$TMP/map.txt" "$SYSRE" 1)
SYSB=$(label_nth "$TMP/map.txt" "$SYSRE" 2)
echo "   discovered: $NSYS systems; A=[$SYSA] B=[$SYSB]"

if [ -z "$SYSA" ]; then
  skip "no star system in the map tree - the whole map leg is unreachable"
else
  # ---- system A expanded ---------------------------------------------------------------
  if tland "$SYSA"; then
    at "on-system-a"
    inp ui.right                       # expand A (the camera flies in) and step in
    pause 2500
    at "system-a-expanded"
    dump 02-system-a-expanded
    ghosts "galaxy, first system expanded"

    # ---- focused tooltip pass (Class-backed carriers) --------------------------------
    snap "$TMP/expanded.txt"
    AKEY=$(key_nth "$TMP/expanded.txt" "$SYSRE" 1)
    delay0
    # The system's dossier is `tooltip/0`, and it is its region's FIRST row -- its own text
    # is the system name, which type-ahead resolves to the system row instead. So land on
    # the SECOND tooltip row by text and step up onto the dossier.
    TIPLBL=$(label_nth "$TMP/expanded.txt" "$AKEY/tooltip/[0-9]*\]" 2)
    if [ -n "$TIPLBL" ] && tland "$TIPLBL"; then
      at "tip-system-tooltip"; tip system-tooltip
      inp ui.up
      at "tip-system-dossier"; tip system-dossier
    else
      skip "the first system declares fewer than two tooltip rows - dossier tooltips not captured"
    fi
    PLANET=$(label_nth "$TMP/expanded.txt" "$AKEY/planet/[0-9]*\]" 1)
    if [ -n "$PLANET" ] && tland "$PLANET"; then
      at "tip-planet-card"; tip planet-card
    else
      skip "the first system has no planet row - planet-card tooltip not captured"
    fi
    LANE=$(label_nth "$TMP/expanded.txt" "$AKEY/lane/[0-9]*\]" 1)
    if [ -n "$LANE" ] && tland "$LANE"; then
      at "tip-starlane"; tip starlane
    else
      skip "the first system has no starlane row - starlane tooltip not captured"
    fi
    delayrestore
  else
    skip "type-ahead could not land on the first system"
  fi

  # ---- system B expanded, then A revisited (the pooled-row shrink leg) -----------------
  if [ -n "$SYSB" ] && tland "$SYSB"; then
    inp ui.right
    pause 2500
    at "system-b-expanded"
    dump 03-system-b-expanded
    ghosts "galaxy, second system expanded"
    if tland "$SYSA"; then
      pause 1500
      at "system-a-revisited"
      dump 04-system-a-revisited
      ghosts "galaxy, first system revisited (pool shrink)"
    else
      skip "could not return to the first system - pool-shrink dump not captured"
    fi
  else
    skip "fewer than two systems in the map tree - second-system and pool-shrink dumps not captured"
  fi
fi

# ---- the HUD's own tooltip carriers --------------------------------------------------
# Row 2 of each HUD region, per the caption rule: which screen buttons and which strategic
# resources a build offers is not something to write down.
inp ui.focusEmpire
snap "$TMP/hud.txt"
delay0
for pair in 'hud:empire/screen/[A-Za-z]*\]|hud-screen-button' 'hud:empire/resource/[A-Za-z0-9]*\]|hud-strategic-resource'; do
  re=$(echo "$pair" | cut -d'|' -f1); lbl=$(echo "$pair" | cut -d'|' -f2)
  txt=$(label_nth "$TMP/hud.txt" "$re" 2)
  if [ -n "$txt" ] && tland "$txt"; then
    at "tip-$lbl"; tip "$lbl"
  else
    skip "no second HUD row matching $re - $lbl tooltip not captured"
  fi
done
delayrestore

# ---- the selected-fleet panel -------------------------------------------------------
inp ui.focusMap
FLEETS=$(fact '((System.Func<string>)(() => { var sb = new System.Text.StringBuilder(); System.Collections.IList fs = (System.Collections.IList)Gui.PlayerEmpire.GetAgency<DepartmentOfDefense>().Fleets; for (int i=0;i<fs.Count;i++){ if (sb.Length>0) sb.Append("|"); sb.Append(((Fleet)fs[i]).LocalizedName); } return sb.ToString(); }))()')
LANDED=""
if [ -n "$FLEETS" ]; then
  OIFS=$IFS; IFS='|'
  for f in $FLEETS; do
    IFS=$OIFS
    if tland "$f"; then LANDED="$f"; break; fi
    IFS='|'
  done
  IFS=$OIFS
fi
if [ -n "$LANDED" ]; then
  echo "   discovered: fleet [$LANDED]"
  at "on-fleet"
  inp ui.activate
  pause 1200
  at "fleet-selected"
  dump 05-fleet-panel
  ghosts "galaxy + selected-fleet panel"
  evs '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow<FleetsScreen>(); if (w != null) w.HandleInput(InputAction.Exit); return "fleet panel closed"; }))()'
  pause 800
else
  skip "no fleet of this empire is reachable on the map tree - selected-fleet panel not captured"
fi

# ---- scan view ----------------------------------------------------------------------
evq "$CS/drain.cs"; pause 1200
evs 'Gui.GuiGameWindowService.ToggleScanView()'; pause 1500
evq "$CS/reset.cs"; pause 600
at "scan-view"
dump 06-scan-view
ghosts "scan view"
evs 'Gui.GuiGameWindowService.ToggleScanView()'; pause 1200

# ---- restore ------------------------------------------------------------------------
evq "$CS/drain.cs"; evq "$CS/tut.cs"; pause 1200
evq "$CS/reset.cs"; pause 600; inp ui.focusMap; inp ui.home
epilogue
