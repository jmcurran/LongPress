<p align="center"><img src="assets/logo-256.png" alt="LongPress logo" width="128"></p>

# LongPress

macOS/Android-style long-press accents for Windows, in any program.

Hold a letter → a strip of variants appears next to the text cursor → press **1–9**
(or arrows/Tab, then Enter/Space, or click) to replace the letter. **Esc** cancels;
any other key closes the strip and types normally.

Covers French, Spanish, German, Dutch, Danish, Czech and Māori by default.

## Install

1. Download `LongPress-Setup-<version>.exe` from the
   [Releases](https://github.com/jmcurran/LongPress/releases) page.
2. Run it. It installs just for you (no administrator rights needed) into
   `%LOCALAPPDATA%\Programs\LongPress`. Nothing else needs to be installed: .NET is
   built in.
3. Windows SmartScreen may warn you, because the installer isn't code-signed.
   Click **More info**, then **Run anyway**.

An "é" icon appears in the notification area. Setup can make LongPress start with
Windows (on by default). You can change this later with **Start with Windows** in the
icon's menu.

To remove it, use Settings > Apps > Installed apps > LongPress > Uninstall. Your
settings in `%APPDATA%\LongPress` are kept.

## Build

You need the .NET 10 SDK (`winget install Microsoft.DotNet.SDK.10`). To build the
installer you also need [Inno Setup 7](https://jrsoftware.org/isinfo.php).

```
dotnet run                    # try it out
dotnet publish -c Release     # self-contained single file: publish\LongPress.exe
.\build-installer.ps1         # publish + installer: installer\Output\LongPress-Setup-<version>.exe
```

The version comes from `<Version>` in `LongPress.csproj`. `build-installer.ps1` finds
`ISCC.exe` in the usual Inno Setup 7 locations (per-user or Program Files). If yours is
somewhere else, run `.\build-installer.ps1 -Iscc <path to ISCC.exe>` or set the `ISCC`
environment variable.

## Using it

| Action | Keys |
|---|---|
| Open the popup | hold the letter |
| Pick a variant | 1–9 (top row or numpad), or click it |
| Move the selection | ← → ↑ ↓, Tab / Shift+Tab |
| Accept the selection | Enter or Space |
| Cancel | Esc |
| Pause / resume | left-click the tray icon |

Capitals: hold Shift (or have Caps Lock on) while holding the letter.

## Settings

Tray menu → **Edit settings…** opens
`%APPDATA%\LongPress\settings.json` in Notepad. After saving, choose **Reload settings**.

- `theme`: `"auto"` (follows Windows light/dark mode), `"light"` or `"dark"`.
- `holdDelayMs`: how long to hold before the popup appears. The popup can't appear
  before Windows starts repeating the key, so the **Repeat delay** slider in
  Control Panel → Keyboard matters too.
- `excludedProcesses`: programs where it's switched off (e.g. `"mstsc"`, games).
- `accents`: character → variants in popup order (max 9; spaces ignored).
  Capitals are generated automatically.

To get the default file back, delete it and choose **Reload settings**.

## Limitations

- Holding a letter that has accents no longer auto-repeats it (same as macOS).
- Programs running as administrator are skipped, unless LongPress is also run as administrator.
- If an app doesn't report its text cursor, the popup appears by the mouse pointer.
- Programs that read the keyboard directly (some games, remote desktop) won't work. Add them to `excludedProcesses`.
