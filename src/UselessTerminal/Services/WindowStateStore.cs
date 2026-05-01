using System.IO;
using System.Text.Json;
using UselessTerminal.Models;
using Path = System.IO.Path;
using File = System.IO.File;

namespace UselessTerminal.Services;

public static class WindowStateStore
{
    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UselessTerminal", "windowstate.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static Models.WindowState Load()
    {
        try
        {
            if (File.Exists(StatePath))
            {
                string json = File.ReadAllText(StatePath);
                return JsonSerializer.Deserialize<Models.WindowState>(json, JsonOptions) ?? new();
            }
        }
        catch { }
        return new();
    }

    public static void Save(Models.WindowState state)
    {
        try
        {
            string? dir = Path.GetDirectoryName(StatePath);
            if (string.IsNullOrEmpty(dir))
                return;

            Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(state, JsonOptions);
            string tempPath = Path.Combine(dir, "windowstate.json.tmp");
            File.WriteAllText(tempPath, json);
            if (File.Exists(StatePath))
                File.Replace(tempPath, StatePath, destinationBackupFileName: null);
            else
                File.Move(tempPath, StatePath);
        }
        catch { }
    }
}
