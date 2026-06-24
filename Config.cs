using System.IO;
using System.Text.Json;

namespace Mixscope;

/// <summary>
/// User-editable settings, persisted to %APPDATA%\mixscope\config.json.
/// </summary>
public sealed class Config
{
    public string VlcHost { get; set; } = "127.0.0.1";
    public int VlcPort { get; set; } = 8080;

    /// <summary>Password set in VLC's Web (Lua HTTP) interface. Username is always blank.</summary>
    public string VlcPassword { get; set; } = "";

    /// <summary>How often to poll VLC, in milliseconds.</summary>
    public int PollMs { get; set; } = 1000;

    /// <summary>Optional explicit path to MediaInfo.exe. Auto-detected when null/empty.</summary>
    public string? MediaInfoPath { get; set; }

    /// <summary>
    /// Screen anchor: TopLeft, TopCenter, TopRight, BottomLeft, BottomCenter, BottomRight,
    /// or Custom (use saved Left/Top). Changed via the right-click Position menu or by dragging.
    /// </summary>
    public string Anchor { get; set; } = "TopRight";

    // Saved overlay position, used only when Anchor == "Custom".
    public double? Left { get; set; }
    public double? Top { get; set; }

    private static string Dir => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "mixscope");

    private static string FilePath => System.IO.Path.Combine(Dir, "config.json");

    public static Config Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Config>(File.ReadAllText(FilePath)) ?? new Config();
        }
        catch { /* fall through to defaults */ }
        return new Config();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* non-fatal */ }
    }

    public static string ConfigFileForDisplay => FilePath;
}
