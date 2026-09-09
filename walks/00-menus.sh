#!/bin/sh
# Family: everything the game draws BEFORE a save is loaded -- the main menu and the pages
# it opens. STANDALONE: walk-all.sh assumes a game is loaded and never runs this one; run
# this against a freshly launched game sitting at the main menu.
#
# Nothing here is pressed that starts, loads, writes or deletes anything: never Start,
# Quick Start, Load, Save, Delete, Apply, Create, Confirm or Exit Game. The new-game lobby
# IS entered -- it is a screen of its own and the door to three more -- and left again with
# the game's own Exit, which is what its Back button calls.
#
# The main menu is where the walk starts and where it ends. Every page is opened the way the
# menu opens it and put away again before the next one, so the fixture the second half of a
# pair meets is the fixture the first half met.
set -u
FAMILY=menus; . "$(dirname "$0")/lib.sh" "$@"

# onscreen <screen key> [timeout-ms] -- block until the mod reports that screen focused.
# A page the menu opens arrives some frames after the click that asked for it -- the lobby is
# a runtime state change and takes seconds -- so every station waits on the mod's own answer
# rather than on a settle guess.
onscreen() {
  printf 'ES2Access.ModEntry.Screens.Current != null && ES2Access.ModEntry.Screens.Current.Key == "%s"' "$1" > "$TMP/on.cs"
  curl -s -X POST --data-binary "@$TMP/on.cs" "$HOST/wait?timeout=${2:-15000}" > "$TMP/on.out" 2>&1
  grep -q '"satisfied":true' "$TMP/on.out"
}

# The shared prologue's drain reads the player empire, which does not exist out of game, so
# this family has two of its own. `mdrain` closes what is on TOP - the modal stack and the
# out-game window list - and is what a station inside the lobby uses, because the lobby has to
# survive it. `backhome` additionally leaves the lobby with the game's own Exit and puts the
# menu back: a page that REPLACES the menu (the DLC browser, the credits) hid it on the way in
# and only its own Exit would have shown it again.
# Twice, both of them: closing the window on top can UNCOVER one that was standing behind it,
# and the window under a message box is only shown again a frame after the box goes.
mdrain()    { evq "$CS/menudrain.cs"; pause 900; evq "$CS/menudrain.cs"; pause 1200; }
backhome()  { evq "$CS/menuhome.cs"; pause 900; evq "$CS/menuhome.cs"; pause 1200; onscreen screen.main-menu 40000 || echo "   NOTE: the main menu did not come back after $1"; }
mprologue() { backhome start; evq "$CS/reset.cs"; pause 900; }
mepilogue() { backhome epilogue; at epilogue; echo "$FAMILY route done"; }

# findland <text> [stops] -- land on a node by type-ahead, walking the screen's stops until one
# of them holds it. A search reads the FOCUSED stop only, and these pages are several stops wide
# (a new-game category, a faction-choice band), so a plain tland finds only what happens to share
# the stop the cursor was reset onto. Deterministic: it always starts from the reset cursor and
# always steps forward.
# The counters here and in optiontabs are prefixed: sh has no local variables, and a plain `i`
# in a helper silently rewrites the caller's loop counter.
findland() {
  fl_i=0
  while [ "$fl_i" -lt "${2:-12}" ]; do
    tland "$1" && return 0
    inp ui.next
    fl_i=$((fl_i+1))
  done
  return 1
}

# tkey <dumpfile> <key ERE> <n> -- land on the nth node matching the key, by the label it
# reads back. Keys are mod-authored and stable; labels are localized, so the walk reads one
# and types it. Non-zero when there is no such node, or no stop holds it.
tkey() {
  t=$(label_nth "$1" "$2" "$3")
  [ -n "$t" ] || return 1
  findland "$t"
}

# optiontabs <file prefix> <label> -- dump an options window one tab at a time and leave it on
# the first. Both windows this family opens are the game's options modal - the mod's own settings
# are a subclass of it - so both are read the same way, by the tab keys the screen declares rather
# than by counted arrows: a capture resets the cursor, and the tab a counted step would land on
# depends on which stop the reset left it in.
optiontabs() {
  ot_pfx="$1"; ot_lbl="$2"
  snap "$TMP/ot.txt"
  # Counted the same way the rows are indexed, so the loop can never ask for a tab key_nth
  # cannot answer: nkeys counts LINES, and key_nth indexes MATCHES.
  ot_n=$(grep -oE '\[options:tab/[^]]*\]' "$TMP/ot.txt" | wc -l | tr -d ' ')
  echo "   discovered: $ot_n tabs on $ot_lbl"
  ot_i=1
  while [ "$ot_i" -le "$ot_n" ]; do
    ot_c=$(key_nth "$TMP/ot.txt" 'options:tab/[^]]*\]' "$ot_i" | sed 's|.*/||')
    if tkey "$TMP/ot.txt" 'options:tab/[^]]*\]' "$ot_i"; then
      inp ui.activate; pause 1800
      capture "$ot_pfx-$ot_c" "$ot_lbl, $ot_c"
    else
      skip "$ot_lbl tab $ot_c could not be landed on"
    fi
    ot_i=$((ot_i+1))
  done
  # left on the first tab: both windows remember the selected one across opens
  if [ "$ot_n" -ge 1 ] && tkey "$TMP/ot.txt" 'options:tab/[^]]*\]' 1; then inp ui.activate; pause 1500; fi
}

mprologue

# ---- the main menu itself ----------------------------------------------------------------
capture 01-main-menu "main menu"
snap "$TMP/mm.txt"
echo "   discovered: $(nkeys "$TMP/mm.txt" 'mainmenu:[^]]*\]') main-menu entries"

# ---- the mod's own settings, through the menu entry it added -------------------------------
MODSET=$(label_of "$TMP/mm.txt" 'mainmenu:mod-settings')
if [ -n "$MODSET" ] && tland "$MODSET"; then
  echo "   discovered: menu entry [$MODSET]"
  inp ui.activate
  # The mod's window IS the game's options modal with the mod's content in it, so the screen it
  # focuses is the options screen; only the name it speaks says which of the two is up.
  if onscreen screen.options 20000; then
    pause 2500
    optiontabs 02-mod-settings-tab "mod settings"
  else
    skip "the mod's settings window did not open - no settings tab dumped"
  fi
else
  skip "the main menu declares no mod-settings entry - no settings tab dumped"
fi
backhome "the mod settings"

# ---- the new-game lobby, and the pages it opens ---------------------------------------------
snap "$TMP/mm.txt"
NEWGAME=$(label_of "$TMP/mm.txt" 'mainmenu:MainMenuNewGame')
if [ -n "$NEWGAME" ] && tland "$NEWGAME"; then
  echo "   discovered: menu entry [$NEWGAME]"
  inp ui.activate
  if onscreen screen.new-game 60000; then
    pause 3000
    capture 04-new-game "new-game lobby"
    snap "$TMP/ng.txt"

    # faction choice -- the empire slot's own portrait button
    if tkey "$TMP/ng.txt" 'newgame:empire/change\]' 1 && { inp ui.activate; onscreen screen.faction-choice 20000; }; then
      pause 2500
      capture 05-faction-choice "faction choice"
      snap "$TMP/fc.txt"

      # the custom-faction editor, only where a key route reaches it. The band's own first row
      # is Delete, so this asks for the create button by name rather than for "a row of the band".
      ADDFAC=$(label_of "$TMP/fc.txt" 'faction-choice:custom/AddFactionButton')
      if [ -n "$ADDFAC" ] && findland "$ADDFAC" && { inp ui.activate; onscreen screen.custom-faction 20000; }; then
        pause 2500
        capture 06-custom-faction "custom-faction editor"
      else
        skip "no key route from faction choice into the custom-faction editor"
      fi
      mdrain; onscreen screen.new-game 20000 || echo "   NOTE: the lobby did not come back after faction choice"
    else
      skip "the lobby declares no faction-choice button - faction choice and the custom-faction editor not captured"
      mdrain
    fi

    # advanced settings -- one button per game-setup category; the first the lobby offers
    snap "$TMP/ng.txt"
    if tkey "$TMP/ng.txt" 'newgame:[^]]*/advanced\]' 1 && { inp ui.activate; onscreen screen.advanced-settings 20000; }; then
      pause 2500
      capture 07-advanced-settings "advanced game settings"
      mdrain; onscreen screen.new-game 20000 || echo "   NOTE: the lobby did not come back after advanced settings"
    else
      skip "the lobby declares no advanced-settings button"
      mdrain
    fi
  else
    skip "the new-game lobby did not open - it and the pages it opens not captured"
  fi
else
  skip "the main menu declares no new-game entry - the lobby and everything it opens not captured"
fi
backhome "the lobby"

# ---- load/save, in the mode the menu opens it in (nothing is loaded, written or deleted) -----
evs '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow("OutGameLoadModalWindow") as LoadSaveModalWindow; if (w == null) return "NO WINDOW"; w.LoadSaveMode = LoadSaveModalWindow.LoadSaveType.Load; Gui.GuiService.ShowWindow(w); return "loadsave shown=" + w.Shown; }))()'
if onscreen screen.load-save 20000; then
  pause 2000
  capture 08-load-save "load/save (Load, from the menu)"
else
  skip "the out-game load window did not open"
fi
backhome "load/save"

# ---- the game's own options, tab by tab -------------------------------------------------------
# Unlike the in-game family, this one DOES select tabs: it selects every one it found and ends on
# the first, so the tab the modal remembers is the same on both halves of a pair.
evs '((System.Func<string>)(() => { var w = Gui.GuiService.GetWindow<OptionsModalWindow>(); if (w == null) return "NO WINDOW"; w.OutGameSkin = true; Gui.GuiService.ShowWindow(w); return "options shown=" + w.Shown; }))()'
if onscreen screen.options 20000; then
  pause 2200
  capture 09-game-options "game options, as opened"
  optiontabs 10-game-options-tab "game options"
else
  skip "the game options modal did not open"
fi
backhome "the options"

# ---- the DLC browser ---------------------------------------------------------------------------
openwin DLCModalWindow 2500
if onscreen screen.dlc 15000; then capture 11-dlc "DLC browser"; else skip "the DLC browser did not open"; fi
backhome "the DLC browser"

# ---- the credits -------------------------------------------------------------------------------
openwin CreditScreen 2500
if onscreen screen.credits 15000; then capture 12-credits "credits"; else skip "the credits did not open"; fi
backhome "the credits"

# ---- the boot disclaimer, if this session can still raise it -----------------------------------
openwin DisclaimerModalWindow 2500
if onscreen screen.disclaimer 15000; then capture 13-disclaimer "disclaimer"; else skip "the disclaimer did not open"; fi
backhome "the disclaimer"

# ---- every registered mod screen, by key -------------------------------------------------------
# The safety net, and the same trick 09-bykey plays in game: a bogus key 400s with the whole
# registry, so the list is read out of the refusal rather than written down. Out of game most keys
# answer the stable one-line "screen inactive: ..."; the menu's own pages answer their content.
: > "$OUT/index.txt"
curl -s -o "$TMP/keys.out" "$HOST/gui/graph?screen=walks-probe-no-such-screen"
KEYS=$(sed 's/.*registered screens are: //; s/"}.*//' "$TMP/keys.out" | tr ',' '\n' | sed 's/^ *//; s/ *$//' | grep '^screen\.')
N=$(echo "$KEYS" | grep -c .)
echo "   discovered: $N registered screen keys"
if [ "$N" -lt 2 ]; then
  skip "could not read the screen registry out of a bogus-key refusal - by-key sweep empty"
else
  for k in $KEYS; do
    curl -s -o "$OUT/bykey.$k.txt" "$HOST/gui/graph?screen=$k&buffers=1"
    printf '%-40s %8s bytes  %s\n' "$k" "$(wc -c < "$OUT/bykey.$k.txt" | tr -d ' ')" \
      "$(head -c 60 "$OUT/bykey.$k.txt" | tr -d '\n')" >> "$OUT/index.txt"
  done
  wc -l < "$OUT/index.txt"
fi

mepilogue
