using System.Text.Json;

namespace TeamsBusyLight;

public class AppSettings
{
    public string ClientId { get; set; } = "";
    public string ComPort { get; set; } = "";
    public int PollIntervalSeconds { get; set; } = 5;

    private static readonly string SettingsPath = Path.Combine(
        AppContext.BaseDirectory, "settings.json");

    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return new AppSettings();
        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch { return new AppSettings(); }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}
