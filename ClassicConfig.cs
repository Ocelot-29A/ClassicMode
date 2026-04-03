using System.IO;
using System.Text.Json;
using MegaCrit.Sts2.Core.Logging;

namespace ClassicModeMod;

public static class ClassicConfig
{
    private static readonly string ConfigPath = Path.Combine(
        Path.GetDirectoryName(typeof(ClassicConfig).Assembly.Location)!,
        "classic_config.json");

    private static bool _classicCards;
    private static bool _classicRelics;

    public static bool ClassicCards
    {
        get => _classicCards;
        set { _classicCards = value; Save(); }
    }

    public static bool ClassicRelics
    {
        get => _classicRelics;
        set { _classicRelics = value; Save(); }
    }

    public static void Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var data = JsonSerializer.Deserialize<ConfigData>(json);
                if (data != null)
                {
                    _classicCards = data.ClassicCards;
                    _classicRelics = data.ClassicRelics;
                }
                Log.Info($"[ClassicMode] Config loaded: Cards={_classicCards}, Relics={_classicRelics}");
            }
        }
        catch (Exception ex)
        {
            Log.Error($"[ClassicMode] Failed to load config: {ex.Message}");
        }
    }

    private static void Save()
    {
        try
        {
            var data = new ConfigData { ClassicCards = _classicCards, ClassicRelics = _classicRelics };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Log.Error($"[ClassicMode] Failed to save config: {ex.Message}");
        }
    }

    private class ConfigData
    {
        public bool ClassicCards { get; set; }
        public bool ClassicRelics { get; set; }
    }
}
