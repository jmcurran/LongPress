namespace LongPress;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Only one copy may run at a time (two keyboard hooks would fight).
        using var mutex = new Mutex(true, @"Local\LongPressAccents", out bool isFirst);
        if (!isFirst) return;

        ApplicationConfiguration.Initialize();

        TrayApp app;
        try
        {
            app = new TrayApp();
        }
        catch (Exception ex)
        {
            MessageBox.Show("LongPress could not start:\n\n" + ex.Message, "LongPress",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        Application.Run(app);
    }
}
