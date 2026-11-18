# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

LongPress is a Windows tray app (C#, WinForms, .NET 10) that adds macOS-style long-press accents to every program. You hold a letter, a strip of variants appears at the text cursor, and you pick one with 1–9, arrows/Tab plus Enter/Space, or a click. `README.md` is the user-facing documentation (keys, settings, limitations). Keep it in step with any change in behaviour.

## Commands

Run these from the repository root. They need the .NET 10 SDK.

```sh
dotnet build
dotnet run                      # runs it for real: installs a system-wide keyboard hook
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

- There are no tests and no linter.
- Only one copy can run at a time (a named mutex in `Program.cs`). Quit the tray copy before `dotnet run`, or the new one exits silently.
- `bin/`, `obj/` and `publish/` are build output. There is no `.gitignore` yet, so don't commit them.

## Architecture

Everything runs on the single UI (STA) thread: the low-level hook callback, popup mouse clicks and the 200 ms foreground-watch timer. Nothing is locked, and none is needed, as long as it stays that way.

- `TrayApp` (an `ApplicationContext`) owns everything. It creates `PopupForm`, `LongPressEngine` and `KeyboardHook`, and runs the tray menu, the HKCU `Run` key ("Start with Windows") and settings reloads.
- `KeyboardHook` wraps `WH_KEYBOARD_LL`. It ignores injected input (`LLKHF_INJECTED`), so the app's own `SendInput` never feeds back into it. Exceptions are swallowed so that none escapes the callback. The handler returns `true` to swallow a key.
- `LongPressEngine` is the state machine:
  1. A first key-down always types normally. If the key's character has variants, they are remembered.
  2. Auto-repeat key-downs for that key are swallowed. Once `HoldDelayMs` has passed, the popup opens.
  3. While the popup is open, digits, arrows, Tab, Enter, Space and Esc are handled and swallowed. Any other key closes the popup and goes through as a normal key-down.

  A commit sends Backspace plus the chosen character as `KEYEVENTF_UNICODE` (`Native.SendReplacement`).
- **Don't do slow work inside the hook callback.** Windows drops hooks that take too long, so the callback queues popup work with `Post` (`BeginInvoke`). The caret lookup in particular is slow in some apps. Queued actions check `_generation` and do nothing if the popup has closed since. Keep this pattern when adding popup actions.
- When a key-down is swallowed, its VK goes into `_swallowKeyUp` so that the matching key-up is swallowed too.
- `Native.CharForKey` uses `ToUnicodeEx` with flag `0x4` and the foreground thread's keyboard layout. The flag stops it from disturbing dead-key state in the target app.
- `ShouldSkip` caches, per process ID, the process name and whether the process runs at a higher integrity level (`Native.IsOutOfReach`, since an app can't inject into elevated processes). It also checks `excludedProcesses`.
- `CaretLocator` first asks for the Win32 caret (`GetGUIThreadInfo`), then the accessibility caret (`OBJID_CARET`, used by Chrome, Electron and similar apps). If both fail it returns null, and the popup appears at the mouse pointer.
- `PopupForm` is a borderless, topmost window that never takes focus, so the target app keeps the keyboard. It draws itself, scales for DPI, and has light and dark themes.
- `Settings` is stored at `%APPDATA%\LongPress\settings.json`, which allows comments and trailing commas. The commented default file is the `DefaultJson` constant. It is written on first run and is also the fallback if the file can't be read. Variant lists are capped at 9 and duplicates are removed. Capitals are worked out from the lowercase entry unless an uppercase key is given, and letters with no single-letter capital (such as ß) are dropped.
- All P/Invoke declarations are in `Native.cs`.
