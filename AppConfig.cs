using System.Text.Json;

internal sealed class AppConfig
{
    public int RefreshSeconds { get; set; } = 30;
    public string[] GroupIds { get; set; } = [];
    public HashSet<string> BlockedUsers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    internal static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TeamsIO", "connection.json");

    public string? Validate()
    {
        if (!Guid.TryParse(TenantId, out var tenant) || tenant == Guid.Empty)
            return "Enter a valid tenant ID (GUID).";
        if (!Guid.TryParse(ClientId, out var client) || client == Guid.Empty)
            return "Enter a valid application (client) ID (GUID).";
        if (GroupIds.Length == 0 || GroupIds.Any(id => !Guid.TryParse(id, out var group) || group == Guid.Empty))
            return "Enter at least one valid group ID (GUID).";
        if (RefreshSeconds < 5 || RefreshSeconds > 3600)
            return "Refresh interval must be between 5 and 3600 seconds.";
        return null;
    }

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath)) return new();
        try
        {
            var config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath)) ?? new();
            config.GroupIds ??= [];
            config.BlockedUsers = new(config.BlockedUsers ?? [], StringComparer.OrdinalIgnoreCase);
            return config;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show("Unable to read saved connection settings. Please configure them again.\n\n" + ex.Message,
                "TeamsIO configuration", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return new();
        }
    }

    public void Save()
    {
        if (Validate() is string error) throw new InvalidDataException(error);
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
        var temporary = ConfigPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, ConfigPath, overwrite: true);
    }
}
