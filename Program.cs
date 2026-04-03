using System.Windows.Forms;

namespace DesktopClock;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) => LogException(args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                LogException(exception);
            }
        };

        ApplicationConfiguration.Initialize();
        using var context = new DesktopClockApplicationContext();
        Application.Run(context);
    }

    private static void LogException(Exception exception)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "startup-error.log");
            File.WriteAllText(logPath, exception.ToString());
        }
        catch
        {
        }
    }
}
