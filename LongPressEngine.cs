using System.Diagnostics;

namespace LongPress;

/// <summary>
/// The long-press logic. Everything here runs on the UI thread: the hook callback,
/// mouse clicks on the popup, and the foreground-watch timer.
///
/// How it works (same as macOS):
///  1. A key is pressed: it types normally. If it has accented variants we remember them.
///  2. The key is held: Windows sends auto-repeat key-downs. We swallow those, and once
///     the key has been held for HoldDelayMs we show the popup.
///  3. With the popup open: 1–9, arrows/Tab + Enter/Space, or a mouse click picks a
///     variant; we send Backspace + the variant. Esc cancels. Any other key closes the
///     popup and types normally.
/// </summary>
internal sealed class LongPressEngine
{
    private readonly PopupForm _popup;

    public Settings Settings { get; set; }
    public bool Enabled { get; set; } = true;

    // The key currently held (the most recent key-down), to recognise auto-repeat.
    private uint _heldVk;
    private long _heldSince;
    private string? _heldVariants;

    // Popup state.
    private bool _popupOpen;
    private string _variants = "";
    private int _selected = -1;
    private int _generation;
    private IntPtr _target;

    // Keys whose key-down we swallowed; swallow their key-up too.
    private readonly HashSet<uint> _swallowKeyUp = new();

    // pid -> (process name, out of reach?)
    private readonly Dictionary<uint, (string Name, bool OutOfReach)> _processes = new();

    public LongPressEngine(PopupForm popup, Settings settings)
    {
        _popup = popup;
        Settings = settings;
        _popup.CellClicked += i =>
        {
            if (_popupOpen && i < _variants.Length) Commit(i);
        };
    }

    /// <summary>Hook handler. Returns true to swallow the key.</summary>
    public bool OnKey(KeyInfo k)
    {
        if (!k.Down)
        {
            if (k.Vk == _heldVk) _heldVk = 0;
            return _swallowKeyUp.Remove(k.Vk);
        }
        return _popupOpen ? OnPopupKeyDown(k) : OnKeyDown(k);
    }

    private bool OnKeyDown(KeyInfo k)
    {
        if (Native.IsModifier(k.Vk)) return false;

        if (k.Vk == _heldVk)
        {
            // Auto-repeat.
            if (_heldVariants == null) return false; // not one of ours: let it repeat
            if (Environment.TickCount64 - _heldSince >= Settings.HoldDelayMs) OpenPopup(_heldVariants);
            return true;
        }

        // A fresh key press.
        _heldVk = k.Vk;
        _heldSince = Environment.TickCount64;
        _heldVariants = null;

        if (!Enabled || k.AltDown
            || Native.IsDown(Native.VK_CONTROL) || Native.IsDown(Native.VK_MENU)
            || Native.IsDown(Native.VK_LWIN) || Native.IsDown(Native.VK_RWIN))
            return false;

        IntPtr fg = Native.GetForegroundWindow();
        if (ShouldSkip(fg)) return false;

        if (Native.CharForKey(k.Vk, k.ScanCode, fg) is char c)
            _heldVariants = Settings.VariantsFor(c);

        return false; // the original character is always typed
    }

    private bool OnPopupKeyDown(KeyInfo k)
    {
        uint vk = k.Vk;
        if (vk == _heldVk) return true;            // still holding the original key
        if (Native.IsModifier(vk)) return false;   // Shift etc. don't close the popup

        int digit = vk is >= Native.VK_1 and <= Native.VK_9 ? (int)(vk - Native.VK_1)
                  : vk is >= Native.VK_NUMPAD1 and <= Native.VK_NUMPAD9 ? (int)(vk - Native.VK_NUMPAD1)
                  : -1;
        if (digit >= 0)
        {
            _swallowKeyUp.Add(vk);
            if (digit < _variants.Length) Commit(digit);
            return true;
        }

        switch (vk)
        {
            case Native.VK_LEFT:
            case Native.VK_UP:
                Move(-1);
                _swallowKeyUp.Add(vk);
                return true;

            case Native.VK_RIGHT:
            case Native.VK_DOWN:
                Move(+1);
                _swallowKeyUp.Add(vk);
                return true;

            case Native.VK_TAB:
                Move(Native.IsDown(Native.VK_SHIFT) ? -1 : +1);
                _swallowKeyUp.Add(vk);
                return true;

            case Native.VK_RETURN:
            case Native.VK_SPACE:
                if (_selected >= 0)
                {
                    _swallowKeyUp.Add(vk);
                    Commit(_selected);
                    return true;
                }
                Close();
                return false;

            case Native.VK_ESCAPE:
                Close();
                _swallowKeyUp.Add(vk);
                return true;

            default:
                Close();
                return OnKeyDown(k); // type it normally (it may start its own long-press)
        }
    }

    private void OpenPopup(string variants)
    {
        _popupOpen = true;
        _variants = variants;
        _selected = -1;
        _target = Native.GetForegroundWindow();
        int generation = ++_generation;
        IntPtr target = _target;

        // Finding the caret can take a moment in some apps, so do it outside the hook callback.
        Post(() =>
        {
            if (generation != _generation) return; // closed in the meantime
            _popup.ShowAt(CaretLocator.Locate(target), variants, Native.DpiFor(target), UseDarkTheme());
        });
    }

    private void Move(int direction)
    {
        int n = _variants.Length;
        if (n == 0) return;
        _selected = _selected < 0
            ? (direction > 0 ? 0 : n - 1)
            : ((_selected + direction) % n + n) % n;
        int selected = _selected;
        int generation = _generation;
        Post(() =>
        {
            if (generation == _generation) _popup.SetSelected(selected);
        });
    }

    private void Commit(int index)
    {
        char ch = _variants[index];
        Close();
        Post(() => Native.SendReplacement(ch));
    }

    public void Close()
    {
        if (!_popupOpen) return;
        _popupOpen = false;
        _selected = -1;
        _generation++;
        Post(() => _popup.Hide());
    }

    /// <summary>Called by a timer: close the popup if the user switched windows.</summary>
    public void CheckForeground()
    {
        if (_popupOpen && Native.GetForegroundWindow() != _target) Close();
    }

    private bool UseDarkTheme() => (Settings.Theme ?? "auto").Trim().ToLowerInvariant() switch
    {
        "dark" => true,
        "light" => false,
        _ => PopupForm.SystemIsDark(), // "auto"
    };

    /// <summary>Runs an action on the UI thread after the hook callback has returned.</summary>
    private void Post(Action action) => _popup.BeginInvoke(action);

    private bool ShouldSkip(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero) return true;
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return true;

        if (!_processes.TryGetValue(pid, out var info))
        {
            if (_processes.Count > 256) _processes.Clear();
            string name;
            try { name = Process.GetProcessById((int)pid).ProcessName; }
            catch { name = ""; }
            info = (name, Native.IsOutOfReach(pid));
            _processes[pid] = info;
        }

        return info.OutOfReach || Settings.IsExcluded(info.Name);
    }
}
