# Installation

## Requirements

- A running screen reader on Windows. The mod speaks through whichever screen reader is active, and falls back to SAPI if none is.

## Installing

1. Close Endless Space 2.
2. Download the [latest release](https://github.com/Neurrone/endless-space-2-access/releases/latest) zip.
3. Extract it into the game's installation folder — the folder that holds `EndlessSpace2.exe` — so that `winhttp.dll` ends up next to `EndlessSpace2.exe`.

## Verifying

Start the game. Once it has loaded you should hear:

```text
Endless Space 2 Access 0.1.0 ready
```

## Settings

The mod writes its own settings file the first time you run it, at
`BepInEx\config\endless.space2.access.cfg`. Edit it with the game closed — the file is read at
start-up, so a change takes effect the next time you launch.

Each setting carries its own explanation in the file above it. The one most worth knowing about
is `cutsceneDescriptions` under `[Speech]`, on by default, which describes what the game's
cutscene videos show. The others cap the frame rate and force a render resolution, both for
cutting the processor load on a machine with no graphics acceleration.

## Updating

Download the newer release and extract it over the game folder again, overwriting what is there.
