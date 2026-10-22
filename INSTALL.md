# Axe Path — Install Guide

Axe Path is distributed as unsigned player builds from the
[Releases page](https://github.com/meschinidino/axepath/releases).
Pick the zip for your OS below. No installer, no admin rights needed.

> Replace `v0.2.0` with the release you want; the file names stay the same.

## macOS (Apple Silicon and Intel)

1. Download **AxePath-macOS.zip** from the release and unzip it
   (double-click in Finder). You get `AxePath.app`.
2. macOS will refuse to open it on first launch ("unidentified
   developer") because the build isn't notarized. Bypass once:
   - Right-click (or Ctrl-click) `AxePath.app` → **Open** → **Open**.
   - Or strip the download flag in Terminal:
     `xattr -dr com.apple.quarantine AxePath.app`
3. Double-click `AxePath.app` to play. The valley builds behind a
   loading screen, then drops you at the spawn stone.

To move it somewhere permanent (e.g. `/Applications`), copy the `.app`
with Finder or `ditto` (plain `cp -R` can damage app bundles):

```sh
ditto AxePath.app /Applications/AxePath.app
```

## Windows 10/11 (64-bit)

1. Download **AxePath-Windows.zip** and unzip it (right-click →
   **Extract All**). You get a folder with `AxePath.exe` plus data
   files — keep them together.
2. Double-click `AxePath.exe` to play.
3. First launch may show a Windows SmartScreen warning ("Unknown
   publisher") because the build is unsigned: click **More info** →
   **Run anyway**. (Only do this for builds you downloaded from the
   Releases page above.)

## Controls

- **WASD / arrows** — move; **mouse** — look
- **Left click** — swing the axe (hold nothing; one click, one chop)
- **Space** — hop (clears a wolf lunge at the apex)
- **E** — read runes/notes in range; **Esc** — pause menu
- Pause menu: mouse sensitivity, volume slider, mute toggle

## Troubleshooting

- **Game froze when I clicked away**: by design, losing focus opens
  the pause menu. Click back in and press Esc to resume.
- **Where are saves/settings?** Volume, mute, sensitivity, and saves
  persist in the OS player-prefs store, so they survive reinstalls
  of the unzipped build.
- **macOS: "The application can't be opened"** after copying with
  `cp -R`: re-copy with `ditto` (see above) — `cp` breaks the bundle
  structure.
- **Windows: missing DLL errors**: install the
  [Visual C++ Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist)
  (x64) and retry.
