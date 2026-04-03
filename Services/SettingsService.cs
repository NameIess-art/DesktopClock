using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using DesktopClock.Models;

namespace DesktopClock.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly string _settingsDirectoryPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopClock");

    private readonly string _settingsFilePath;

    public SettingsService()
    {
        _settingsFilePath = Path.Combine(_settingsDirectoryPath, "settings.json");
    }

    public ClockSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return ClockSettings.CreateDefault();
            }

            var json = File.ReadAllText(_settingsFilePath);
            var settings = JsonSerializer.Deserialize<ClockSettings>(json, JsonOptions) ?? ClockSettings.CreateDefault();
            settings.Normalize();
            return settings;
        }
        catch
        {
            return ClockSettings.CreateDefault();
        }
    }

    public void Save(ClockSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(_settingsDirectoryPath);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsFilePath, json);
    }
}
