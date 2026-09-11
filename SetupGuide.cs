using System.Text.RegularExpressions;

internal static class SetupGuide
{
    public const string DesktopDocumentationUrl = "https://learn.microsoft.com/en-us/entra/identity-platform/scenario-desktop-app-configuration";
    public static string Markdown
    {
        get
        {
            using var stream = typeof(SetupGuide).Assembly.GetManifestResourceStream("TeamsIO.SetupGuide")!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
    public static string PlainText => Regex.Replace(Markdown, @"(?m)^#{1,6} ", "");
}
