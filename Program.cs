using System.Diagnostics;


internal static class Program
{

    private static Mutex? _instanceMutex;
    [STAThread]
    static void Main(string[] args)
    {
        if (!ClosePreviousInstanceFromSamePath()) return;
        AppLog.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        Application.ThreadException += (s, e) =>
        {
            Debug.WriteLine(e.Exception);
            AppLog.Error("Unhandled WinForms exception.", e.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                Debug.WriteLine(ex);
                AppLog.Error("Unhandled application exception.", ex);
            }
        };

        _instanceMutex = new Mutex(
            true,
            @"Local\TeamsIOBoard_SingleInstance",
            out bool createdNew
        );

        if (!createdNew)
        {
            // A previous process can briefly retain the mutex while Windows is
            // finishing its shutdown. Avoid starting two message loops.
            return;
        }

        var defaultProxy = System.Net.WebRequest.DefaultWebProxy;
        if (defaultProxy is not null)
        {
            defaultProxy.Credentials =
                System.Net.CredentialCache.DefaultNetworkCredentials;
            System.Net.Http.HttpClient.DefaultProxy = defaultProxy;
        }

        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var config = AppConfig.Load();
            if (config.Validate() is not null)
            {
                using var setup = new ConnectionSettingsForm(config);
                if (setup.ShowDialog() != DialogResult.OK) return;
                config = AppConfig.Load();
            }
            // The setup dialog may install a WinForms synchronization context.
            // Authenticate on the thread pool so blocking here cannot deadlock
            // continuations waiting for that now-closed modal message loop.
            // The board and clipboard still run on this original STA thread.
            var service = Task.Run(() => TeamsIoService.CreateAsync(config))
                .GetAwaiter()
                .GetResult();



            var form = new BoardForm(service, config);

            BoardHelpers.Theme.ApplyTitleBarTheme(form);

            Application.Run(form);
        }
        catch (Exception ex)
        {
            AppLog.Error("Application startup failed.", ex);
            var changeSettings = MessageBox.Show(
                "TeamsIO could not start.\n\n" + ex.Message + "\n\nOpen connection settings?",
                "TeamsIO Startup Failure", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
            if (changeSettings == DialogResult.Yes)
            {
                using var settings = new ConnectionSettingsForm(AppConfig.Load());
                if (settings.ShowDialog() == DialogResult.OK) Application.Restart();
            }
        }
    }

    private static bool ClosePreviousInstanceFromSamePath()
    {
        using var current = Process.GetCurrentProcess();
        var currentPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(currentPath))
            return true;

        foreach (var process in Process.GetProcessesByName(current.ProcessName))
        {
            using (process)
            {
                if (process.Id == current.Id || !IsSameExecutable(process, currentPath))
                    continue;

                try
                {
                    if (process.MainWindowHandle != IntPtr.Zero)
                        process.CloseMainWindow();

                    if (!process.WaitForExit(2000))
                    {
                        MessageBox.Show("TeamsIO is still closing. Please try again in a moment.", "TeamsIO");
                        return false;
                    }
                }
                catch (InvalidOperationException)
                {
                    // The previous process exited between discovery and shutdown.
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    AppLog.Error("Could not close the previous TeamsIO process.", ex);
                }
            }
        }
        return true;
    }

    private static bool IsSameExecutable(Process process, string currentPath)
    {
        try
        {
            return string.Equals(
                process.MainModule?.FileName,
                currentPath,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (System.ComponentModel.Win32Exception) { return false; }
        catch (InvalidOperationException) { return false; }
    }
}
