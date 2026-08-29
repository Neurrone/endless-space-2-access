# Endless Space 2 Access on macOS

The mod runs on the Mac version of the game. Everything here is macOS-only; nothing about the
Windows build changes.

## What is different on a Mac

- **Speech is the macOS system voice.** The mod speaks it itself: each line is rendered with
  the system speech synthesizer and played back-to-back through the mod's own audio engine, so
  queued lines (notifications, chat, the save spinner, buffer review) follow each other with no
  gap, and a key press cuts speech at once. The voice and speaking rate are the ones you set
  under System Settings, Accessibility, Spoken Content ("System voice" and "Speaking rate");
  change them there and restart the game. VoiceOver is not needed and is not used: its
  announcement API cannot queue, so every queued line would cut off the one before it. You can
  leave VoiceOver running; the game window has no accessibility tree for it to read.
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
- Known gap: the mod's own settings window shows key rows in the game's spelling of a
  combination (Alt, LeftCommand); what is spoken elsewhere says Option and Cmd.

## Installing

1. Launch Endless Space 2 once from Steam without the mod. The game installs the Mono
   framework it needs on that first launch and quits; launch it again and quit from the menu.
2. Download BepInEx 5.4.23.5 for macOS, `BepInEx_macos_universal_5.4.23.5.zip`, from
   <https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5>, and unzip its contents into the
   game folder, the one that CONTAINS `EndlessSpace2.app`:
   `~/Library/Application Support/Steam/steamapps/common/Endless Space 2/`. Afterwards that
   folder holds `BepInEx/`, `run_bepinex.sh`, `libdoorstop.dylib` and `.doorstop_version` next to
   the .app.
3. Copy the mod's `BepInEx/plugins/ES2Access/` folder into `BepInEx/plugins/`.
4. Clear the quarantine flag macOS puts on downloaded libraries, or the game will refuse to
   load them: in Terminal, `xattr -dr com.apple.quarantine "$HOME/Library/Application Support/Steam/steamapps/common/Endless Space 2"`.
5. In Steam, open the game's Properties, Launch Options, and enter:
   `"$HOME/Library/Application Support/Steam/steamapps/common/Endless Space 2/run_bepinex.sh" %command%`
   with `$HOME` written out as your home folder (`/Users/yourname`).
6. Launch from Steam. The mod announces itself once the main menu is up.

Logs: `<game folder>/BepInEx/LogOutput.log` for the mod, `~/Library/Logs/Unity/Player.log` for
the game. The game's saves live in `~/Library/Application Support/Endless Space 2/`.

## Building and testing on a Mac

- Prerequisites: the .NET SDK, Steam with the game installed and launched once, and BepInEx
  unpacked next to the .app as above (the build references `BepInEx/core`).
- Copy `GamePaths.props.template` to `GamePaths.props` and adjust the macOS block's `GameDir`
  if your install is elsewhere; the Windows block may stay, each side reads only its own.
- `dotnet build ES2Access/ES2Access.csproj` builds and deploys into the game folder exactly as
  on Windows. Build outputs go to `bin/mac` and `obj/mac` so a checkout shared with a Windows
  machine keeps the two builds apart.
- `./run-game.sh [--no-build] [--no-speech] [--no-dev] [--no-wait] [--load-save "<title>"]`
  launches the game through `run_bepinex.sh` with the dev server on; Steam must be running.
  `./wait-game.sh <menu|ingame|loading|dialog>` blocks until the game is there. Both are twins
  of the PowerShell scripts and take the same arguments.
- `dotnet test ES2Access.Tests/ES2Access.Tests.csproj` runs the offline tests.
- The dev server's `POST /key` (real OS key events) is Windows-only and answers a refusal on
  macOS; `POST /input` runs actions everywhere.
