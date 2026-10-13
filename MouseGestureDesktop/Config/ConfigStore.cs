using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MouseGestureDesktop.Config;

/// <summary>Loads/saves <see cref="GestureConfig"/> to %APPDATA%\MouseGestureDesktop\config.json.</summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public string ConfigPath { get; }

    public ConfigStore()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MouseGestureDesktop");
        Directory.CreateDirectory(dir);
        ConfigPath = Path.Combine(dir, "config.json");
    }

    public GestureConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                GestureConfig? cfg = JsonSerializer.Deserialize<GestureConfig>(json, JsonOptions);
                if (cfg is not null)
                    return cfg;
            }
        }
        catch
        {
            // Corrupt/unreadable config → fall back to defaults rather than crashing the tray app.
        }

        return new GestureConfig();
    }

    public void Save(GestureConfig config)
    {
        string json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(ConfigPath, json);
    }
}
