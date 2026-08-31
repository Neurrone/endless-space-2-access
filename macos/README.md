# Endless Space 2 Access on macOS

The mod runs on the Mac version of the game. Everything here is macOS-only; nothing about the
Windows build changes.

## What is different on a Mac

- **Speech is the macOS system voice.** The mod speaks it itself: each line is rendered with
  the system speech synthesizer and played back-to-back through the mod's own audio engine, so
  queued lines (notifications, chat, the save spinner, buffer review) follow each other with no
  gap, and a key press cuts speech at once. Out of the box the voice and speaking rate are the
  ones you set under System Settings, Accessibility, Spoken Content ("System voice" and
  "Speaking rate"), and they follow that setting across restarts until you pick your own.
  VoiceOver is not needed and, normally, not used: its announcement API cannot queue, so every
  queued line would cut off the one before it. You can leave VoiceOver running; the game window
  has no accessibility tree for it to read. If the system voice cannot be started at all, the
  mod falls back to the Prism speech library (`libprism.dylib` next to the .app), which speaks
  through VoiceOver when it is running; the log (`BepInEx/LogOutput.log`) says which backend is
  speaking.
- **The Speech tab in Mod settings.** The "Mod settings" entry (right after Options on the main
  menu and the pause menu) has a macOS-only Speech tab: the backend (System voice, or VoiceOver
  through Prism if you prefer VoiceOver's own speech and can live without the queue), the
  voice — the list offers the voices for the game's language, with your Spoken Content voice
  as the default first entry — and the speaking rate and speech volume. Changes take effect as
  you make them; Apply keeps them, Cancel puts everything back, and "Reset voice and rate to
  Spoken Content" returns to following the OS setting.
- **The chord modifiers are Option and Command.** Every key the manual writes as `Ctrl+X` is
  `Option+X` on a Mac, and every `Alt+X` is `Cmd+X`; the letters do not change. So: Option+H
  the empire banners, Option+N the notifications, Option+E the turn controls, Option+T the turn
  log, Option+G the galaxy map, Option+M the map summary, Option+Enter the Control-click,
  Option+Shift+Enter the Alt-click, Option+Cmd+Enter the double click, Option+Cmd+E end turn,
  Option+Cmd+F next idle fleet, Option+Cmd+A apply movements, Cmd+Up/Down regions,
  Cmd+Left/Right page turn, Option+arrows, Option+Home/End and Option+PageUp/PageDown the
  review buffer. The reason: on macOS Control+arrows belong to the desktop (Spaces, Mission
  Control) and Control+Option is VoiceOver's own modifier. The game's chat key moves to
  Option+Tab (the game's options draw it as Alt + Tab). The mod's spoken hints say "Option"
  and "Cmd" accordingly.
- Cmd+Q quits the game at once, with no prompt. On a laptop keyboard Home, End, PageUp and
  PageDown are Fn+Left, Fn+Right, Fn+Up and Fn+Down.
- The mod's own settings window speaks its key rows in the same spelling ("Option+Shift+Enter",
  "Cmd+Up Arrow"); the text DRAWN in those cells is still the game's own ("Alt", "LeftCommand"),
  which only a sighted onlooker meets.

## Installing

1. Launch Endless Space 2 once from Steam without the mod. The game installs the Mono
   framework it needs on that first launch and quits; launch it again and quit from the menu.
2. Download the mod's macOS release zip (`EndlessSpace2Access-macOS-v<version>.zip`), unzip it
   anywhere, and in Terminal run `./install.sh` from the unzipped folder. It places the BepInEx
   runtime, the mod, the launcher and the Prism fallback library next to the .app, clears the
   quarantine flag macOS puts on downloaded libraries, and prints the exact launch option line.
   If the game is not in Steam's usual place, pass the game folder (the one that CONTAINS
   `EndlessSpace2.app`) as the argument.
3. In Steam, open the game's Properties, Launch Options, and paste the line the installer
   printed - `"<game folder>/run-modded.sh" %command%`, quotes included.
4. Launch from Steam. The mod announces itself once the main menu is up.

Installing by hand instead: unzip the BepInEx 5.4.23.5 macOS build
(`BepInEx_macos_universal_5.4.23.5.zip` from
<https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5>) into the game folder; copy the
mod's `BepInEx/plugins/ES2Access/` into `BepInEx/plugins/`, and `run-modded.sh` and
`libprism.dylib` next to the .app; `chmod +x run-modded.sh`; clear quarantine with
`xattr -dr com.apple.quarantine "<game folder>"`; set the same launch option. The mod brings
its own launcher because BepInEx's `run_bepinex.sh` cannot start this game's elderly Mono
correctly - the header of `run-modded.sh` explains the three problems it fixes.
`libprism.dylib` is the fallback speech backend; the game runs without it, at the cost of
speech if the system voice ever fails.

Logs: `<game folder>/BepInEx/LogOutput.log` for the mod, `~/Library/Logs/Unity/Player.log` for
the game. The game's saves live in `~/Library/Application Support/Endless Space 2/`.

## Building and testing on a Mac

- Prerequisites: the .NET SDK, Steam with the game installed and launched once, and BepInEx
  unpacked next to the .app as above (the build references `BepInEx/core`).
- Copy `GamePaths.props.template` to `GamePaths.props` and adjust the macOS block's `GameDir`
  if your install is elsewhere; the Windows block may stay, each side reads only its own.
- `dotnet build ES2Access/ES2Access.csproj` builds and deploys into the game folder exactly as
  on Windows. Build outputs go to `bin/mac` and `obj/mac` so a checkout shared with a Windows
  machine keeps the two builds apart. The first build downloads the pinned Prism release into
  the gitignored `prism-build/` (the one step that needs the network) and deploys
  `libprism.dylib` next to the .app.
- `./run-game.sh [--no-build] [--no-speech] [--no-dev] [--no-wait] [--load-save "<title>"]`
  launches the game through the deployed `run-modded.sh` with the dev server on; Steam must be
  running. `./wait-game.sh <menu|ingame|loading|dialog>` blocks until the game is there. Both
  are twins of the PowerShell scripts and take the same arguments. The launch goes through
  launchd's gui domain: a game started straight from an SSH login cannot reach the Eloquence
  voices, and speech would come out in the compact default voice instead of the Spoken Content
  one.
- `dotnet test ES2Access.Tests/ES2Access.Tests.csproj` runs the offline tests.
- `./build_release.sh` builds the player-facing macOS zip into `releases/` - the Release
  build, the pinned BepInEx and Prism downloads (cached in gitignored `bepinex-build/` and
  `prism-build/`), `install.sh` and this README staged together. Twin of `build_release.ps1`,
  which keeps building the Windows zip.
- The dev server's `POST /key` (real OS key events) is Windows-only and answers a refusal on
  macOS; `POST /input` runs actions everywhere.
