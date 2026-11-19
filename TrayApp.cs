using System.Diagnostics;
using Microsoft.Win32;

namespace LongPress;

/// <summary>Notification-area icon and menu; owns the hook, popup and engine.</summary>
internal sealed class TrayApp : ApplicationContext
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValue = "LongPress";

    private readonly PopupForm _popup;
    private readonly LongPressEngine _engine;
    private readonly KeyboardHook _hook;
    private readonly System.Windows.Forms.Timer _watch;
    private readonly NotifyIcon _tray;
    private readonly ToolStripMenuItem _enabledItem;
    private readonly ToolStripMenuItem _startupItem;
    private readonly Icon _onIcon = LoadIcon("LongPress.ico");
    private readonly Icon _offIcon = LoadIcon("LongPress-paused.ico");

    public TrayApp()
    {
        var (settings, error) = Settings.Load();

        _popup = new PopupForm();
        _ = _popup.Handle; // create the window now so BeginInvoke works

        _engine = new LongPressEngine(_popup, settings);
        _hook = new KeyboardHook(_engine.OnKey);
        _hook.Install();

        _watch = new System.Windows.Forms.Timer { Interval = 200 };
        _watch.Tick += (_, _) => _engine.CheckForeground();
        _watch.Start();

        _enabledItem = new ToolStripMenuItem("Enabled", null, (_, _) => ToggleEnabled()) { Checked = true };
        _startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleStartup())
        {
            Checked = IsStartupEnabled()
        };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Edit settings…", null, (_, _) => OpenSettings());
        menu.Items.Add("Reload settings", null, (_, _) => ReloadSettings());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => ExitThread());

        _tray = new NotifyIcon
        {
            Icon = _onIcon,
            Text = "LongPress accents",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ToggleEnabled();
        };

        if (error != null) _tray.ShowBalloonTip(8000, "LongPress", error, ToolTipIcon.Warning);
    }

    private void ToggleEnabled()
    {
        _engine.Enabled = !_engine.Enabled;
        if (!_engine.Enabled) _engine.Close();
        _enabledItem.Checked = _engine.Enabled;
        _tray.Icon = _engine.Enabled ? _onIcon : _offIcon;
        _tray.Text = _engine.Enabled ? "LongPress accents" : "LongPress accents (paused)";
    }

    private void OpenSettings()
    {
        Settings.Load(); // makes sure the file exists
        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{Settings.FilePath}\""));
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "LongPress", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ReloadSettings()
    {
        var (settings, error) = Settings.Load();
        _engine.Settings = settings;
        if (error != null)
            _tray.ShowBalloonTip(8000, "LongPress", error, ToolTipIcon.Warning);
        else
            _tray.ShowBalloonTip(2000, "LongPress", "Settings reloaded.", ToolTipIcon.Info);
    }

    private static bool IsStartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(RunValue) != null;
    }

    private void ToggleStartup()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (IsStartupEnabled())
            key.DeleteValue(RunValue, false);
        else
            key.SetValue(RunValue, $"\"{Environment.ProcessPath}\"");
        _startupItem.Checked = IsStartupEnabled();
    }

    /// <summary>Loads an embedded .ico at the tray size for the current DPI.</summary>
    private static Icon LoadIcon(string resourceName)
    {
        using var stream = typeof(TrayApp).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource {resourceName}.");
        return new Icon(stream, SystemInformation.SmallIconSize);
    }

    protected override void ExitThreadCore()
    {
        _hook.Dispose();
        _watch.Stop();
        _watch.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _popup.Dispose();
        base.ExitThreadCore();
    }
}
