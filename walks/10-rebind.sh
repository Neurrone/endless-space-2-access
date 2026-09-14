#!/bin/sh
# Family: A-B-A rebind proofs for the surfaces the game pools.
#
# A pooled widget keeps whatever the previous binding left on its components, so a dump
# taken on a fresh binding proves only the fresh case. Each leg here reads the surface
# (A), performs the game's own rebinding action onto a SECOND subject (B), returns, reads
# it again (A'), and diffs A against A'. A node whose availability, hint, label or tooltip
# differs between A and A' is carrying B's state.
#
# Two rules make the legs mechanical rather than judgement:
#   * B must SHRINK the bound list before it grows back, since surplus items are what a
#     pool retires and leaves behind. Which subject is the smaller one is discovered at
#     runtime - planet counts, action counts, section row counts - and never written down.
#   * every capture prints Alpha beside Visible for the pool's own children, because a
#     retired child parked at Alpha 0 draws no text, is pruned out of a graph dump, and
#     the dump then agrees with whatever the mod declared.
#
# NOT COVERED, and why:
#   * the economy tab bar (MarketplaceTabToggle) - its rebind is researching the
#     marketplace technology with the screen open, which spends the save.
#   * the negotiation shelf and basket (TermLine, ContributionTermLine) - reaching them
#     means opening the negotiation modal, and closing an unsigned negotiation posts an
#     order.
#   * the senate and election candidate cards - their rebind is stepping the election
#     through its phases, which costs turns.
#   * the hacking program menus (HackingProgramLine) - the menus exist only with a hacking
#     target selected in scan view and a console mode to switch between, a state the walk
#     may not manufacture.
set -u
FAMILY=rebind; . "$(dirname "$0")/lib.sh" "$@"

# ---------------------------------------------------------------- capture primitives

# unjson <file> -- an /eval reply's `result` as the text it is. A failed eval does not
# match the prefix and is left whole, so the failure lands in the artifact.
unjson() { sed 's/^{"ok":true,"result":"//; s/","error":.*$//' "$1" | sed 's/\\n/\n/g; s/\\"/"/g'; }

# gwalk <surface> <part> <leg> <key substring> -- the mod's own reading of every node whose
# key contains the substring, with its review buffer, through an /eval walk of the render
# rather than GET /gui/graph: these screens run past the dump's 800-line cap, and a capture
# that truncates differently on the two sides of a pair proves nothing. The walk expands
# the subtree it is about to read, so a collapsed card is still in the record.
gwalk() {
  sed "s|@@TARGET@@|$4|" "$CS/rebind-walk.cs" > "$TMP/gw.cs"
  curl -s -X POST --data-binary "@$TMP/gw.cs" "$HOST/eval?speech=0" > "$TMP/gw.out"
  unjson "$TMP/gw.out" > "$OUT/$1-$2-$3.txt"
  printf '%8s  %s/%s-%s-%s\n' "$(wc -c < "$OUT/$1-$2-$3.txt" | tr -d ' ')" "$FAMILY" "$1" "$2" "$3"
}

# gpool <surface> <part> <leg> <C# that assigns `t`> -- Visible AND Alpha per pooled child,
# with the first few label texts under it as the bound subject. This is the half a graph
# dump cannot show.
gpool() {
  sed "s|@@CONTAINER@@|$4|" "$CS/rebind-pool.cs" > "$TMP/gp.cs"
  curl -s -X POST --data-binary "@$TMP/gp.cs" "$HOST/eval?speech=0" > "$TMP/gp.out"
  unjson "$TMP/gp.out" > "$OUT/$1-$2-$3.txt"
  printf '%8s  %s/%s-%s-%s\n' "$(wc -c < "$OUT/$1-$2-$3.txt" | tr -d ' ')" "$FAMILY" "$1" "$2" "$3"
}

# aba <surface> <part> [part...] -- diff each part's A against its A'. The normaliser is
# walks/normalize.sed and nothing else: the classes that legitimately move between two
# reads of the same surface are already its list - the HUD wall clock, the
# GetHashCode-derived sheet / droplist / ship / design ids, and DevProbe.Tooltip's
# accumulating defaultRead. A difference that survives it is stale pooled state.
aba() {
  s=$1; shift; : > "$OUT/$s.aba.txt"; n=0
  for part in "$@"; do
    a="$OUT/$s-$part-a.txt"; b="$OUT/$s-$part-a2.txt"
    if [ ! -f "$a" ] || [ ! -f "$b" ]; then
      printf '%s %s: not captured on both legs\n' "$s" "$part" >> "$OUT/$s.aba.txt"; continue
    fi
    sed -f "$WALKS_DIR/normalize.sed" "$a" > "$TMP/na.txt"
    sed -f "$WALKS_DIR/normalize.sed" "$b" > "$TMP/nb.txt"
    d=$(diff "$TMP/na.txt" "$TMP/nb.txt" | grep -c '^[<>]')
    n=$((n+d))
    if [ "$d" -gt 0 ]; then
      printf '===== %s %s : %s differing lines (A vs A-prime) =====\n' "$s" "$part" "$d" >> "$OUT/$s.aba.txt"
      diff "$TMP/na.txt" "$TMP/nb.txt" | cut -c1-300 >> "$OUT/$s.aba.txt"
    fi
  done
  [ -s "$OUT/$s.aba.txt" ] || printf 'A == A-prime for: %s\n' "$*" > "$OUT/$s.aba.txt"
  printf '   A-B-A %-26s %s differing lines\n' "$s" "$n"
}

# tlands <text> -- land on a node by type-ahead, stepping stops until one of them holds it.
# Two things a plain tland gets wrong here and reads back as "the fixture has no such row":
# POST /type searches the FOCUSED stop only, and a reset cursor does not start on the stop
# the target is in; and the search drops the parentheses out of the query while matching
# against the drawn text, so a label like `Foo (Bar)` never matches itself - the fallback
# is the part of the label before its first bracket.
tlands() {
  alt=$(printf '%s' "$1" | sed 's/ *(.*$//')
  s=0
  while [ "$s" -lt 12 ]; do
    tland "$1" && return 0
    if [ -n "$alt" ] && [ "$alt" != "$1" ]; then tland "$alt" && return 0; fi
    inp ui.next; s=$((s+1))
  done
  return 1
}

# The subject with the largest / smallest count, out of `key:count:label` records joined
# by `|`. This is how B is chosen so that the pool shrinks.
biggest()  { echo "$1" | tr '|' '\n' | sort -t: -k2,2nr | head -1; }
smallest() { echo "$1" | tr '|' '\n' | sort -t: -k2,2n  | head -1; }
rkey()   { echo "$1" | cut -d: -f1; }
rcount() { echo "$1" | cut -d: -f2; }
rlabel() { echo "$1" | cut -d: -f3-; }

prologue

# ==================================================================================
# 1. Star-system page planet cards (PlanetLabel_SystemManagement), rebound by turning the
#    page to another system. A page turn calls RequestStarSystemManagementViewLevel, which
#    is what is issued here so that the destination can be the system with FEWER planets.
# ==================================================================================
SYSCARDS='t = Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemManagement>(false).PlanetLabelsContainer;'
SYSREC=$(fact '((System.Func<string>)(() => { var sb=new System.Text.StringBuilder(); System.Collections.IList css=(System.Collections.IList)Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>().ColonizedStarSystems; for(int i=0;i<css.Count;i++){ var n=((ColonizedStarSystem)css[i]).Node; System.Collections.IList ps=(System.Collections.IList)n.Planets; if(sb.Length>0) sb.Append("|"); sb.Append(n.GUID).Append(":").Append(ps==null?0:ps.Count).Append(":").Append(n.LocalizedName); } return sb.ToString(); }))()')
SBIG=$(biggest "$SYSREC"); SSML=$(smallest "$SYSREC")
TWOSYS=0
if [ -n "$SYSREC" ] && [ "$(rkey "$SBIG")" != "$(rkey "$SSML")" ]; then TWOSYS=1; fi
echo "   discovered: $(echo "$SYSREC" | tr '|' '\n' | grep -c .) colonized system(s); A has $(rcount "$SBIG") planets, B has $(rcount "$SSML")"

sysopen_guid() {
  evs "((System.Func<string>)(() => { System.Collections.IList css=(System.Collections.IList)Gui.PlayerEmpire.GetAgency<DepartmentOfTheInterior>().ColonizedStarSystems; for(int i=0;i<css.Count;i++){ var n=((ColonizedStarSystem)css[i]).Node; if(n.GUID.ToString()==\"$1\"){ Gui.GuiGameWindowService.RequestStarSystemManagementViewLevel(n.GUID); return \"opened \"+n.LocalizedName; } } return \"SYSTEM NOT FOUND\"; }))()"
  pause 3000; evq "$CS/tut.cs"; pause 800
}

if [ "$TWOSYS" -ne 1 ]; then
  skip "the empire owns fewer than two colonized systems - the star-system planet-card rebind is unreachable"
else
  sysopen_guid "$(rkey "$SBIG")"
  at "sys-cards-A"
  gwalk sys-cards graph a 'system:planet'
  gpool sys-cards pool  a "$SYSCARDS"
  ghosts "star system page, the largest owned system (A)"

  sysopen_guid "$(rkey "$SSML")"       # B: fewer planets, so the card pool retires some
  at "sys-cards-B"
  gpool sys-cards poolb b "$SYSCARDS"

  sysopen_guid "$(rkey "$SBIG")"
  at "sys-cards-A-prime"
  gwalk sys-cards graph a2 'system:planet'
  gpool sys-cards pool  a2 "$SYSCARDS"
  ghosts "star system page, the largest owned system revisited (A-prime)"
  aba sys-cards graph pool
fi

evq "$CS/drain.cs"; evq "$CS/tut.cs"; pause 1500; evq "$CS/reset.cs"; pause 800

# ==================================================================================
# 2. Map orbital planet cards (PlanetLabel_SystemOrbital), rebound by flying the camera
#    into another system and back. The cards exist only for the system the camera has come
#    in on, so the rebind is the map's own expand gesture rather than an eval.
# ==================================================================================
ORBCARDS='t = Gui.GuiService.GetWindow<PlanetLabelsWindow_SystemOrbital>(false).PlanetLabelsContainer;'
inp ui.focusMap
snap "$TMP/stop.txt"
CONST=$(label_nth "$TMP/stop.txt" 'galaxy:constellation/[0-9]*\]' 1)
if [ -n "$CONST" ] && tlands "$CONST"; then inp ui.right; else inp ui.home; fi
pause 1500
snap "$TMP/map.txt"
SYSRE='galaxy:[^]]*/system/[0-9]*\]'
MAPA=$(label_nth "$TMP/map.txt" "$SYSRE" 1); MKEYA=$(key_nth "$TMP/map.txt" "$SYSRE" 1)
MAPB=$(label_nth "$TMP/map.txt" "$SYSRE" 2); MKEYB=$(key_nth "$TMP/map.txt" "$SYSRE" 2)

# flyto <label> <key> -- expand the system's row, which flies the camera to orbital zoom,
# and echo how many planet cards the map then draws for it.
flyto() {
  inp ui.focusMap
  tlands "$1" || { echo 0; return 1; }
  inp ui.right; pause 3000
  sed "s|@@TARGET@@|$2|" "$CS/rebind-walk.cs" > "$TMP/gw.cs"
  curl -s -X POST --data-binary "@$TMP/gw.cs" "$HOST/eval?speech=0" > "$TMP/gw.out"
  unjson "$TMP/gw.out" | grep -c '/planet/[0-9]*\]'
}

if [ -z "$MAPA" ] || [ -z "$MAPB" ]; then
  skip "fewer than two systems in the map tree - the orbital planet-card rebind is unreachable"
else
  NA=$(flyto "$MAPA" "$MKEYA"); NB=$(flyto "$MAPB" "$MKEYB")
  echo "   discovered: the first map system draws $NA orbital cards, the second draws $NB"
  if [ "${NA:-0}" -eq 0 ] && [ "${NB:-0}" -eq 0 ]; then
    skip "neither of the first two map systems draws an orbital planet card - the orbital rebind is unreachable"
  else
    if [ "${NB:-0}" -gt "${NA:-0}" ]; then
      OA=$MAPB; OKA=$MKEYB; OB=$MAPA; OKB=$MKEYA
    else
      OA=$MAPA; OKA=$MKEYA; OB=$MAPB; OKB=$MKEYB
    fi
    flyto "$OA" "$OKA" > /dev/null
    at "orbital-cards-A"
    gwalk orbital-cards graph a "$OKA"
    gpool orbital-cards pool  a "$ORBCARDS"
    ghosts "galaxy map, the orbital cards of the larger system (A)"

    flyto "$OB" "$OKB" > /dev/null
    at "orbital-cards-B"
    gpool orbital-cards poolb b "$ORBCARDS"

    flyto "$OA" "$OKA" > /dev/null
    at "orbital-cards-A-prime"
    gwalk orbital-cards graph a2 "$OKA"
    gpool orbital-cards pool  a2 "$ORBCARDS"
    ghosts "galaxy map, the same system flown back into (A-prime)"
    aba orbital-cards graph pool
  fi
fi

evq "$CS/drain.cs"; evq "$CS/tut.cs"; pause 1500; evq "$CS/reset.cs"; pause 800

# ==================================================================================
# 3. The selected fleet's action panel (FleetActionItem), rebound by selecting another
#    fleet. The action set differs in KIND between fleets - a toggle action against a
#    button action - which is the rebind the game's hint write skips a branch on.
# ==================================================================================
FLEETACTS='t = Gui.GuiService.GetWindow<FleetsScreen>(false).FleetActionsPanel.FleetActionsTable;'
FLEETS=$(fact '((System.Func<string>)(() => { var sb = new System.Text.StringBuilder(); System.Collections.IList fs = (System.Collections.IList)Gui.PlayerEmpire.GetAgency<DepartmentOfDefense>().Fleets; for (int i=0;i<fs.Count;i++){ if (sb.Length>0) sb.Append("|"); sb.Append(((Fleet)fs[i]).LocalizedName); } return sb.ToString(); }))()')

# selfleet <name> -- select the fleet on the map and echo how many action buttons the
# panel then draws. Non-zero exit when type-ahead cannot reach it.
selfleet() {
  # The panel a previous selection left up owns the focused screen, and type-ahead would
  # search it instead of the map tree.
  evs '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow<FleetsScreen>(); if (w != null && w.Shown) w.HandleInput(InputAction.Exit); return "fleet panel closed"; }))()'
  pause 800
  inp ui.focusMap
  tlands "$1" || return 1
  inp ui.click; pause 1800
  sed "s|@@TARGET@@|fleets:action/|" "$CS/rebind-walk.cs" > "$TMP/gw.cs"
  curl -s -X POST --data-binary "@$TMP/gw.cs" "$HOST/eval?speech=0" > "$TMP/gw.out"
  unjson "$TMP/gw.out" | sed -n 's/^nodes=//p'
}

FA=""; FB=""; CA=0; CB=0
if [ -n "$FLEETS" ]; then
  OIFS=$IFS; IFS='|'
  for f in $FLEETS; do
    IFS=$OIFS
    if c=$(selfleet "$f"); then
      if [ -z "$FA" ]; then FA=$f; CA=${c:-0}
      elif [ "$f" != "$FA" ]; then FB=$f; CB=${c:-0}; break
      fi
    fi
    IFS='|'
  done
  IFS=$OIFS
fi
if [ -z "$FA" ] || [ -z "$FB" ]; then
  skip "fewer than two of this empire's fleets are reachable on the map tree - the fleet-action rebind is unreachable"
else
  echo "   discovered: two fleets drawing $CA and $CB action buttons"
  if [ "${CB:-0}" -gt "${CA:-0}" ]; then SWAP=$FA; FA=$FB; FB=$SWAP; fi   # A is the richer panel
  selfleet "$FA" > /dev/null
  at "fleet-actions-A"
  gwalk fleet-actions graph a 'fleets:action/'
  gpool fleet-actions pool  a "$FLEETACTS"
  ghosts "selected-fleet panel, the fleet with more actions (A)"

  selfleet "$FB" > /dev/null
  at "fleet-actions-B"
  gpool fleet-actions poolb b "$FLEETACTS"

  selfleet "$FA" > /dev/null
  at "fleet-actions-A-prime"
  gwalk fleet-actions graph a2 'fleets:action/'
  gpool fleet-actions pool  a2 "$FLEETACTS"
  ghosts "selected-fleet panel, the same fleet reselected (A-prime)"
  aba fleet-actions graph pool
fi

evq "$CS/drain.cs"; evq "$CS/tut.cs"; pause 1500; evq "$CS/reset.cs"; pause 800

# ==================================================================================
# 4. Empire-page planet cards (PlanetCard), rebound by selecting another row of the
#    systems table. B is the system with the fewest planets, so the card pool retires
#    some before it grows back.
# ==================================================================================
EMPCARDS='t = Gui.GuiService.GetWindow<EmpireScreen>(false).StarSystemsManagementPanel.StarSystemPlanetCardsPanel.PlanetCardsTable;'
# selrow <system name> -- select that row of the systems table and open the planet-cards
# panel on it. The panel is opened through the game's own opener rather than by clicking a
# cell: which cell opens which detail is decided by the cell's TYPE, and one of them opens
# a hero modal.
selrow() {
  evq "$CS/reset.cs"; pause 600
  tlands "$1" || return 1
  inp ui.click; pause 1500
  evs '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow<EmpireScreen>(false); if (w == null) return "NO EMPIRE SCREEN"; var p = w.StarSystemsManagementPanel; if (p == null || p.GuiTable == null || p.GuiTable.SelectedLine == null) return "NO SELECTED ROW"; var m = typeof(StarSystemsManagementPanel).GetMethod("ShowStarSystemPlanetCardsPanelWithActions", System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance); if (m == null) return "NO OPENER"; m.Invoke(p, null); return "planet cards shown=" + p.StarSystemPlanetCardsPanel.Shown; }))()'
  pause 1800
}
openwin EmpireScreen 2500; evq "$CS/tut.cs"; pause 800
if [ "$TWOSYS" -ne 1 ]; then
  skip "the empire owns fewer than two colonized systems - the empire-page planet-card rebind is unreachable"
elif ! selrow "$(rlabel "$SBIG")"; then
  skip "the systems table has no row for the largest owned system - the empire-page planet-card rebind is unreachable"
else
  at "empire-cards-A"
  gwalk empire-cards graph a 'empire:planet/'
  gpool empire-cards pool  a "$EMPCARDS"
  ghosts "empire page, the largest owned system selected (A)"
  if selrow "$(rlabel "$SSML")"; then
    at "empire-cards-B"
    gpool empire-cards poolb b "$EMPCARDS"
    selrow "$(rlabel "$SBIG")"
    at "empire-cards-A-prime"
    gwalk empire-cards graph a2 'empire:planet/'
    gpool empire-cards pool  a2 "$EMPCARDS"
    ghosts "empire page, the largest owned system reselected (A-prime)"
    aba empire-cards graph pool
  else
    skip "the systems table has no row for the smallest owned system - the empire-page planet-card rebind is unreachable"
  fi
fi
hidewin EmpireScreen; pause 800

# ==================================================================================
# 5. Marketplace section radios (MarketTabRadio) and the item list under them, rebound by
#    selecting another section: that re-Binds every radio in the pool and re-fills the item
#    table. B is the section with the fewest rows.
# ==================================================================================
BUYRADIOS='t = Gui.GuiService.GetWindow<EconomyScreen>(false).MarketplacePanel.BuyableItemsPanel.MarketTabRadiosTable;'
SELLRADIOS='t = Gui.GuiService.GetWindow<EconomyScreen>(false).MarketplacePanel.SalableItemsPanel.MarketTabRadiosTable;'
# The first radio of the band is its region's first row, so its spoken label opens with the
# region's drawn caption and type-ahead can never find its own text. The route therefore
# lands on the SECOND radio by text and reaches the first with ui.left - the radios sit
# side by side, so Left and Right are the steps between them, not Up and Down.
# selsect <label> <extra step or empty> -- select a section, echo how many item rows it fills
selsect() { evq "$CS/reset.cs"; pause 600; tlands "$1" || return 1
            if [ -n "${2:-}" ]; then inp "$2"; fi
            inp ui.click; pause 1800
            snap "$TMP/sec.txt"; nkeys "$TMP/sec.txt" 'economy:buy/row[^]]*\]'; }
openwin EconomyScreen 2500; evq "$CS/tut.cs"; pause 800
evq "$CS/reset.cs"; pause 600
snap "$TMP/eco.txt"
NTAB=$(nkeys "$TMP/eco.txt" 'economy:tab/[0-9]*\]')
echo "   discovered: $NTAB economy tab(s)"
MKT=0; i=1
while [ "$i" -le "${NTAB:-0}" ]; do
  tab=$(label_nth "$TMP/eco.txt" 'economy:tab/[0-9]*\]' "$i")
  if [ -n "$tab" ] && tlands "$tab"; then
    inp ui.click; pause 2000; evq "$CS/reset.cs"; pause 600
    snap "$TMP/eco.txt"
    if [ "$(nkeys "$TMP/eco.txt" 'economy:buy/filter/[0-9]*\]')" -ge 2 ]; then MKT=1; break; fi
  fi
  i=$((i+1))
done

SEC1=$(grep -E '\[economy:buy/filter/0\]' "$TMP/eco.txt")
SEC2=$(grep -E '\[economy:buy/filter/1\]' "$TMP/eco.txt")
SECLBL=$(label_nth "$TMP/eco.txt" 'economy:buy/filter/[0-9]*\]' 2)
if [ "$MKT" -ne 1 ]; then
  skip "no economy tab offers two or more marketplace sections - the marketplace radio rebind is unreachable"
elif [ -z "$SECLBL" ] || printf '%s\n%s\n' "$SEC1" "$SEC2" | grep -q ', unavailable,'; then
  # A section the empire may not trade in is drawn switched off and refuses the selection.
  skip "the first two marketplace sections are not both selectable - the radio rebind is unreachable"
else
  C1=$(selsect "$SECLBL" ui.left); C2=$(selsect "$SECLBL" "")
  echo "   discovered: the first two sections fill $C1 and $C2 item rows"
  if [ "${C1:-0}" -ge "${C2:-0}" ]; then ASTEP=ui.left; BSTEP=""; else ASTEP=""; BSTEP=ui.left; fi
  selsect "$SECLBL" "$ASTEP" > /dev/null
  at "market-radios-A"
  gwalk market-radios buy  a 'economy:buy/'
  gwalk market-radios sell a 'economy:sell/'
  gpool market-radios poolbuy  a "$BUYRADIOS"
  gpool market-radios poolsell a "$SELLRADIOS"
  ghosts "economy marketplace, the section with the most rows (A)"

  selsect "$SECLBL" "$BSTEP" > /dev/null
  at "market-radios-B"
  gpool market-radios poolbuyb b "$BUYRADIOS"

  selsect "$SECLBL" "$ASTEP" > /dev/null
  at "market-radios-A-prime"
  gwalk market-radios buy  a2 'economy:buy/'
  gwalk market-radios sell a2 'economy:sell/'
  gpool market-radios poolbuy  a2 "$BUYRADIOS"
  gpool market-radios poolsell a2 "$SELLRADIOS"
  ghosts "economy marketplace, that section reselected (A-prime)"
  aba market-radios buy sell poolbuy poolsell
fi
hidewin EconomyScreen; pause 800

# ==================================================================================
# 6. Diplomacy ring sectors (EmpireSector). A wedge is never CLICKED - activating one opens
#    the negotiation modal, and closing an unsigned negotiation posts an order - so the
#    rebind here is the screen's own hover selection, which is what fades each empire's
#    detail block in and out, driven by moving the cursor between two wedges.
# ==================================================================================
SECTORS='t = Gui.GuiService.GetWindow<DiplomacyScreen>(false).EmpireSectorsContainer;'
openwin DiplomacyScreen 2500; evq "$CS/tut.cs"; pause 800
evq "$CS/reset.cs"; pause 600
snap "$TMP/dip.txt"
EMPRE='diplomacy:empire/[0-9]*\]'
# Row 1 of the ring carries the region's drawn caption in its spoken label, so the rows
# type-ahead can find start at 2.
DA=$(label_nth "$TMP/dip.txt" "$EMPRE" 2); DB=$(label_nth "$TMP/dip.txt" "$EMPRE" 3)
echo "   discovered: $(nkeys "$TMP/dip.txt" "$EMPRE") empires on the ring"
if [ -z "$DA" ] || [ -z "$DB" ]; then
  skip "fewer than three empires on the diplomacy ring - the sector rebind is unreachable"
elif ! tlands "$DA"; then
  skip "type-ahead could not land on a diplomacy wedge - the sector rebind is unreachable"
else
  pause 1200
  at "sectors-A"
  gwalk sectors graph a 'diplomacy:empire/'
  gpool sectors pool  a "$SECTORS"
  ghosts "diplomacy ring, the first wedge selected (A)"
  if tlands "$DB"; then
    pause 1200
    at "sectors-B"
    gpool sectors poolb b "$SECTORS"
    tlands "$DA"; pause 1200
    at "sectors-A-prime"
    gwalk sectors graph a2 'diplomacy:empire/'
    gpool sectors pool  a2 "$SECTORS"
    ghosts "diplomacy ring, the first wedge reselected (A-prime)"
    aba sectors graph pool
  else
    skip "type-ahead could not land on a second diplomacy wedge - the sector rebind is unreachable"
  fi
fi
hidewin DiplomacyScreen; pause 800
evq "$CS/drain.cs"; evq "$CS/tut.cs"; pause 1200; evq "$CS/reset.cs"; pause 800

# ==================================================================================
# 7. The notification popup's pooled body, rebound by browsing to the next notification and
#    back with the popup's own arrows. A pending notification is a fixture accident the
#    walk may neither create nor destroy: nothing is dismissed, the popup is closed by
#    hiding its window, and the AlreadyRead flag that opening one sets is put back.
# ==================================================================================
NOTIFBODY='{ var ws = UnityEngine.Object.FindObjectsOfType<NotificationWindow>(); for (int q=0;q<ws.Length;q++) if (ws[q].Shown) t = ws[q].gameObject.GetComponent<AgeTransform>(); }'
inp ui.focusNotifications
snap "$TMP/hud.txt"
NNOTIF=$(nkeys "$TMP/hud.txt" 'hud:notification/[0-9]*\]')
echo "   discovered: $NNOTIF pending notification(s)"
READSTATE=$(fact '((System.Func<string>)(() => { var sb=new System.Text.StringBuilder(); System.Collections.IList ns=(System.Collections.IList)Gui.GuiNotificationService.GetPlayerEmpireGuiNotifications(); for(int i=0;i<ns.Count;i++){ if(sb.Length>0) sb.Append("|"); sb.Append(((GuiNotification)ns[i]).AlreadyRead); } return sb.ToString(); }))()')
if [ "${NNOTIF:-0}" -lt 2 ]; then
  skip "the HUD strip holds fewer than two pending notifications - the popup-body rebind is unreachable"
else
  inp ui.home; inp ui.click; pause 2200
  at "notification-A"
  gwalk notification graph a 'notification:'
  gpool notification pool  a "$NOTIFBODY"
  ghosts "notification popup, the notification the strip opened on (A)"

  inp ui.pageNext; pause 2000                      # the popup's own Next arrow
  at "notification-B"
  gpool notification poolb b "$NOTIFBODY"

  inp ui.pagePrev; pause 2000                      # and its Previous arrow, back to A
  at "notification-A-prime"
  gwalk notification graph a2 'notification:'
  gpool notification pool  a2 "$NOTIFBODY"
  ghosts "notification popup, browsed back (A-prime)"
  aba notification graph pool

  evs '((System.Func<string>)(() => { var ws = UnityEngine.Object.FindObjectsOfType<NotificationWindow>(); var sb = new System.Text.StringBuilder(); for (int i=0;i<ws.Length;i++){ if (ws[i].Shown) { sb.Append(ws[i].Name).Append(","); Gui.GuiService.HideWindow(ws[i]); } } return sb.Length==0?"no popup shown":("hid "+sb.ToString()); }))()'
  pause 1500
fi
# Put back the read flags that browsing the popup set, so the strip is left as it was found.
if [ -n "$READSTATE" ]; then
  evs "((System.Func<string>)(() => { string[] want = \"$READSTATE\".Split('|'); System.Collections.IList ns=(System.Collections.IList)Gui.GuiNotificationService.GetPlayerEmpireGuiNotifications(); int n=0; for(int i=0;i<ns.Count && i<want.Length;i++){ var g=(GuiNotification)ns[i]; bool w = want[i]==\"True\"; if(g.AlreadyRead!=w){ g.AlreadyRead=w; n++; } } return \"restored \"+n+\" read flags\"; }))()"
fi

epilogue
