using System.IO;
using System.Text.Json;
using UselessTerminal.Models;
using Path = System.IO.Path;
using File = System.IO.File;

namespace UselessTerminal.Services;

public sealed class SettingsStore
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UselessTerminal", "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static SettingsStore Instance { get; } = new();

    public AppSettings Current { get; private set; } = new();

    public event Action? SettingsChanged;

    private SettingsStore() { }

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                string json = File.ReadAllText(SettingsPath);
                using var doc = JsonDocument.Parse(json);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new();
                AppSettingsLegacyMigration.ApplyIfNeeded(doc.RootElement, settings);
                Current = settings;
            }
        }
        catch
        {
            Current = new();
        }
    }

    public void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(SettingsPath);
            if (dir is not null) Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(Current, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    public void Apply(AppSettings settings)
    {
        Current = settings;
        Save();
        SettingsChanged?.Invoke();
    }

    /// <summary>Updates shell font size from Ctrl+scroll (or similar); clamps 8–32 and persists.</summary>
    public void UpdateFontSize(int fontSize)
    {
        fontSize = Math.Clamp(fontSize, 8, 32);
        if (Current.FontSize == fontSize) return;
        Current.FontSize = fontSize;
        Save();
        SettingsChanged?.Invoke();
    }
}
