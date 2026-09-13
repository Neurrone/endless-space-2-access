# Installation

## Requirements

- A running screen reader on Windows. The mod speaks through whichever screen reader is active, and falls back to SAPI otherwise.

## Missing Gog Localizations

As of version 1.5.75.2, there are missing localization values in the Gog version of the game. This causes raw localization keys to be read on various screens. Until Amplitude Studios fixes this, extract this [zip file](https://github.com/Neurrone/endless-space-2-access/blob/main/gog-translations.zip) to the game's installation folder, the same folder where the mod should be extracted.

## Installing

1. Download the [latest release](https://github.com/Neurrone/endless-space-2-access/releases/latest) zip
2. Extract it into the game's installation folder, where `EndlessSpace2.exe` is, so that `winhttp.dll` ends up next to `EndlessSpace2.exe`.

## Verifying

Start the game. Once it has loaded you should hear:

```text
Endless Space 2 Access x.y.z ready
```

## Updating

Close the game, extract the new release on top of the existing files and restart.
