namespace LongPress;

/// <summary>Finds the text cursor (caret) of the foreground window, in screen coordinates.</summary>
internal static class CaretLocator
{
    public static Rectangle? Locate(IntPtr foreground)
    {
        if (foreground == IntPtr.Zero) return null;
        uint thread = Native.GetWindowThreadProcessId(foreground, out _);

        var info = new Native.GUITHREADINFO
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.GUITHREADINFO>()
        };
        if (!Native.GetGUIThreadInfo(thread, ref info)) return null;

        // 1. Classic Win32 caret (Notepad, most dialogs, Office, many others).
        if (info.hwndCaret != IntPtr.Zero)
        {
            var topLeft = new Native.POINT { X = info.rcCaret.Left, Y = info.rcCaret.Top };
            var bottomRight = new Native.POINT { X = info.rcCaret.Right, Y = info.rcCaret.Bottom };
            if (Native.ClientToScreen(info.hwndCaret, ref topLeft) && Native.ClientToScreen(info.hwndCaret, ref bottomRight))
            {
                return Rectangle.FromLTRB(topLeft.X, topLeft.Y,
                    Math.Max(bottomRight.X, topLeft.X + 1), Math.Max(bottomRight.Y, topLeft.Y + 1));
            }
        }

        // 2. Accessibility caret (Chrome, Edge, Firefox, Electron apps such as VS Code).
        IntPtr focus = info.hwndFocus != IntPtr.Zero ? info.hwndFocus : foreground;
        try
        {
            Guid iid = Native.IID_IAccessible;
            if (Native.AccessibleObjectFromWindow(focus, Native.OBJID_CARET, ref iid, out object? obj) == 0
                && obj is Accessibility.IAccessible acc)
            {
                acc.accLocation(out int left, out int top, out int width, out int height, 0);
                if (height > 0 && (left != 0 || top != 0))
                    return new Rectangle(left, top, Math.Max(width, 1), height);
            }
        }
        catch
        {
            // Not supported by this app; fall through.
        }

        return null; // caller falls back to the mouse position
    }
}
