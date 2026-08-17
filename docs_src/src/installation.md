# Installation

## Requirements

- Windows
- A running screen reader. The mod speaks through whichever screen reader is active, and falls back to SAPI if none is.
- Endless Space 2, from any desktop store. Steam and GOG both work; the mod calls no store API.

## Installing

1. Close Endless Space 2.
2. Download the [latest release](https://github.com/Neurrone/endless-space-2-access/releases/latest) zip.
3. Extract it into the game's installation folder — the folder that holds `EndlessSpace2.exe` — so that `winhttp.dll` ends up next to `EndlessSpace2.exe`.

The zip carries everything the mod needs, including BepInEx 5 (the loader the mod runs on) and `prism.dll`, the speech library, which lands in the game folder beside the exe. There are no settings to configure and no configuration files in the zip.

## Verifying

Start the game. Once it has loaded you should hear:

```text
Endless Space 2 Access 0.1.0 ready
```

If you hear nothing, check that your screen reader is running, and that `winhttp.dll` really is beside `EndlessSpace2.exe` rather than one folder up or down.

## Updating

Download the newer release and extract it over the game folder again, overwriting what is there.

## Uninstalling

Delete the files the zip added: `winhttp.dll`, `prism.dll`, the `BepInEx` folder, and `doorstop_config.ini` if it is present. The game then runs exactly as it did before.
