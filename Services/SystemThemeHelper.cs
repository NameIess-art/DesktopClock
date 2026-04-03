using Microsoft.Win32;

namespace DesktopClock.Services;

public static class SystemThemeHelper
{
    private const string PersonalizeRegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static bool IsDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeRegistryPath, writable: false);
            var systemUsesLightTheme = key?.GetValue("SystemUsesLightTheme");
            if (systemUsesLightTheme is int systemValue)
            {
                return systemValue == 0;
            }

            var appsUseLightTheme = key?.GetValue("AppsUseLightTheme");
            return appsUseLightTheme is int appValue && appValue == 0;
        }
        catch
        {
            return false;
        }
    }
}
