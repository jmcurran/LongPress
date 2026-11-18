using System.ComponentModel;
using System.Runtime.InteropServices;

namespace LongPress;

internal readonly record struct KeyInfo(uint Vk, uint ScanCode, bool Down, bool AltDown);

/// <summary>
/// System-wide low-level keyboard hook. The handler returns true to swallow the key.
/// The callback runs on the thread that installed the hook (our UI thread).
/// </summary>
internal sealed class KeyboardHook : IDisposable
{
    private readonly Native.LowLevelKeyboardProc _proc; // keep a reference so the GC doesn't collect it
    private readonly Func<KeyInfo, bool> _handler;
    private IntPtr _hook;

    public KeyboardHook(Func<KeyInfo, bool> handler)
    {
        _handler = handler;
        _proc = Callback;
    }

    public void Install()
    {
        _hook = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, Native.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
            int msg = (int)wParam;
            bool down = msg is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN;
            bool up = msg is Native.WM_KEYUP or Native.WM_SYSKEYUP;
            bool injected = (data.flags & Native.LLKHF_INJECTED) != 0;

            // Ignore synthetic input, including our own SendInput.
            if (!injected && (down || up))
            {
                try
                {
                    bool altDown = (data.flags & Native.LLKHF_ALTDOWN) != 0;
                    if (_handler(new KeyInfo(data.vkCode, data.scanCode, down, altDown)))
                        return (IntPtr)1;
                }
                catch
                {
                    // Never let an exception escape a hook callback.
                }
            }
        }
        return Native.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hook != IntPtr.Zero)
        {
            Native.UnhookWindowsHookEx(_hook);
            _hook = IntPtr.Zero;
        }
    }
}
