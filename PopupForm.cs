namespace LongPress;

/// <summary>
/// The small strip of accented characters. It never takes keyboard focus,
/// so the app you're typing in stays active.
/// </summary>
internal sealed class PopupForm : Form
{
    private string _chars = "";
    private int _selected = -1;
    private int _cellW = 40, _cellH = 52, _pad = 4, _numH = 14;
    private Font _charFont = new("Segoe UI", 22, GraphicsUnit.Pixel);
    private Font _numFont = new("Segoe UI", 11, GraphicsUnit.Pixel);

    private Color _back, _border, _ink, _muted;

    /// <summary>Raised when a character is clicked with the mouse.</summary>
    public event Action<int>? CellClicked;

    public PopupForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        DoubleBuffered = true;
        ApplyTheme(dark: false);
    }

    /// <summary>True if Windows is set to dark mode for apps (Settings → Personalisation → Colours).</summary>
    public static bool SystemIsDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyTheme(bool dark)
    {
        if (dark)
        {
            _back = Color.FromArgb(32, 32, 32);
            _border = Color.FromArgb(85, 85, 85);
            _ink = Color.FromArgb(240, 240, 240);
            _muted = Color.FromArgb(150, 150, 150);
        }
        else
        {
            _back = Color.FromArgb(250, 250, 250);
            _border = Color.FromArgb(160, 160, 160);
            _ink = Color.FromArgb(20, 20, 20);
            _muted = Color.FromArgb(120, 120, 120);
        }
        BackColor = _back;
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_TOPMOST;
            return cp;
        }
    }

    // We size ourselves for the monitor's DPI; don't let WinForms rescale us again.
    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        e.Cancel = true;
        base.OnDpiChanged(e);
    }

    public void ShowAt(Rectangle? caret, string chars, int dpi, bool dark)
    {
        ApplyTheme(dark);
        _chars = chars;
        _selected = -1;

        float s = dpi / 96f;
        _cellW = (int)(40 * s);
        _cellH = (int)(52 * s);
        _pad = (int)(4 * s);
        _numH = (int)(14 * s);
        _charFont.Dispose();
        _numFont.Dispose();
        _charFont = new Font("Segoe UI", 22 * s, GraphicsUnit.Pixel);
        _numFont = new Font("Segoe UI", 11 * s, GraphicsUnit.Pixel);

        var size = new Size(_cellW * chars.Length + 2 * _pad, _cellH + 2 * _pad);
        Rectangle anchor = caret ?? new Rectangle(Cursor.Position, new Size(1, (int)(20 * s)));
        Rectangle area = Screen.FromPoint(anchor.Location).WorkingArea;
        int gap = (int)(6 * s);

        int x = anchor.Left - _pad;
        int y = anchor.Bottom + gap;
        if (y + size.Height > area.Bottom) y = anchor.Top - gap - size.Height; // no room below: go above
        x = Math.Clamp(x, area.Left, Math.Max(area.Left, area.Right - size.Width));
        y = Math.Clamp(y, area.Top, Math.Max(area.Top, area.Bottom - size.Height));

        Bounds = new Rectangle(new Point(x, y), size);
        if (!Visible) Show();
        Bounds = new Rectangle(new Point(x, y), size); // again, in case a DPI change moved us
        Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0,
            Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
        Invalidate();
    }

    public void SetSelected(int index)
    {
        _selected = index;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(_back);
        using (var pen = new Pen(_border))
            g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

        for (int i = 0; i < _chars.Length; i++)
        {
            var cell = new Rectangle(_pad + i * _cellW, _pad, _cellW, _cellH);
            bool selected = i == _selected;
            if (selected)
            {
                using var brush = new SolidBrush(SystemColors.Highlight);
                g.FillRectangle(brush, cell);
            }

            var charRect = new Rectangle(cell.X, cell.Y, cell.Width, cell.Height - _numH);
            var numRect = new Rectangle(cell.X, cell.Bottom - _numH - _pad / 2, cell.Width, _numH);
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding;

            TextRenderer.DrawText(g, _chars[i].ToString(), _charFont, charRect,
                selected ? SystemColors.HighlightText : _ink, flags);
            TextRenderer.DrawText(g, (i + 1).ToString(), _numFont, numRect,
                selected ? SystemColors.HighlightText : _muted, flags);
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        int i = (e.X - _pad) / Math.Max(_cellW, 1);
        if (e.Button == MouseButtons.Left && i >= 0 && i < _chars.Length)
            CellClicked?.Invoke(i);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _charFont.Dispose();
            _numFont.Dispose();
        }
        base.Dispose(disposing);
    }
}
