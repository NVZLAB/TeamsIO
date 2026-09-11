using Microsoft.Win32;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

internal static class BoardHelpers
{
    public static bool HasConsole =>
        Environment.UserInteractive &&
        !Console.IsOutputRedirected;

    // Theme Management
    public static class Theme
    {
        private const string ThemeKey =
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
        private const string ThemeValue = "AppsUseLightTheme";

        public static bool IsLightMode()
        {
            using var key = Registry.CurrentUser.OpenSubKey(ThemeKey);
            return key?.GetValue(ThemeValue) is int v && v == 1;
        }

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(
            IntPtr hwnd,
            string? subAppName,
            string? subIdList);

        public static void ApplyTitleBarTheme(Form form)
        {
            ApplyTitleBarTheme(form, IsLightMode());
        }

        public static Color GetAccentColor()
        {
            const string dwmKey = @"Software\Microsoft\Windows\DWM";
            using var key = Registry.CurrentUser.OpenSubKey(dwmKey);

            if (key?.GetValue("ColorizationColor") is int rawColor)
            {
                var argb = unchecked((uint)rawColor);
                return Color.FromArgb(
                    (int)((argb >> 16) & 0xFF),
                    (int)((argb >> 8) & 0xFF),
                    (int)(argb & 0xFF));
            }

            return SystemColors.Highlight;
        }

        public static void ApplyTitleBarTheme(Form form, bool light)
        {
            if (Environment.OSVersion.Version.Major < 10)
                return;

            int useDark = light ? 0 : 1;

            DwmSetWindowAttribute(
                form.Handle,
                DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref useDark,
                sizeof(int));
        }

        public static void ApplyControlTheme(Control control, bool light)
        {
            var themeName = light ? "Explorer" : "DarkMode_Explorer";
            SetWindowTheme(control.Handle, themeName, null);

            foreach (Control child in control.Controls)
            {
                if (child is ScrollBar)
                    SetWindowTheme(child.Handle, themeName, null);

                if (child.HasChildren)
                    ApplyControlTheme(child, light);
            }
        }
    }

    public static Color GetPresenceColorWinForms(string? availability)
    {
        return availability switch
        {
            "Available" => Color.LimeGreen,

            "Busy" or
            "BusyIdle" or
            "DoNotDisturb" or
            "InAMeeting" or
            "InACall" or
            "InAConferenceCall" or
            "Presenting" => Color.Red,

            "Away" or
            "Inactive" or
            "BeRightBack" => Color.Gold,

            "OffWork" or
            "Offline" => Color.Gray,

            _ => Color.White
        };
    }

    public static string StripHtml(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "—";

        var noHtml = Regex.Replace(input, "<[^>]*>", string.Empty,
            RegexOptions.NonBacktracking, TimeSpan.FromSeconds(1));
        return WebUtility.HtmlDecode(noHtml).Trim();
    }

    public static string Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text ?? "";

        return text[..(maxLength - 1)] + "…";
    }
}
