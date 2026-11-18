# LongPress

macOS/Android-style long-press accents for Windows, in any program.

Hold a letter → a strip of variants appears next to the text cursor → press **1–9**
(or arrows/Tab, then Enter/Space, or click) to replace the letter. **Esc** cancels;
any other key closes the strip and types normally.

Covers French, Spanish, German, Dutch, Danish, Czech and Māori by default.

## Build

Needs the .NET 10 SDK: `winget install Microsoft.DotNet.SDK.10`

```
cd LongPress
dotnet run                      # try it out
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

The result is `publish\LongPress.exe`. Copy it wherever you like and run it; an "é"
icon appears in the notification area. Use the icon's menu to turn on
**Start with Windows**.

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
