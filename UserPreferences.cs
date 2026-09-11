using System.Text.Json;

internal sealed class UserPreferences
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string Theme { get; set; } = "Auto";
    public bool AlwaysOnTop { get; set; }
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public int? WindowWidth { get; set; }
    public int? WindowHeight { get; set; }
    public bool Maximized { get; set; }
    public string? SortColumn { get; set; }
    public bool SortDescending { get; set; }

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TeamsIO",
        "settings.json");

    public static UserPreferences Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new UserPreferences();

            return JsonSerializer.Deserialize<UserPreferences>(
                File.ReadAllText(SettingsPath),
                JsonOptions) ?? new UserPreferences();
        }
        catch (Exception ex)
        {
            AppLog.Error("Unable to load user preferences.", ex);
            return new UserPreferences();
        }
    }

    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(SettingsPath)!;
            Directory.CreateDirectory(directory);
            File.WriteAllText(
                SettingsPath,
                JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex)
        {
            AppLog.Error("Unable to save user preferences.", ex);
        }
    }
}
