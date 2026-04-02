using System.IO;
using System.Text.Json;
using UselessTerminal.Models;
using Path = System.IO.Path;
using File = System.IO.File;

namespace UselessTerminal.Services;

public static class WtProfileImporter
{
    private static readonly string[] SettingsPaths =
    [
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages", "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState", "settings.json"),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages", "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe", "LocalState", "settings.json"),
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Windows Terminal", "settings.json")
    ];

    public static List<SavedSession> Import()
    {
        var sessions = new List<SavedSession>();

        foreach (string settingsPath in SettingsPaths)
        {
            if (!File.Exists(settingsPath)) continue;

            try
            {
                string json = StripJsonComments(File.ReadAllText(settingsPath));
                using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });

                var root = doc.RootElement;
                if (!root.TryGetProperty("profiles", out var profiles)) continue;

                JsonElement list;
                if (profiles.TryGetProperty("list", out list))
                {
                    foreach (var profile in list.EnumerateArray())
                        TryAddProfile(profile, sessions);
                }
                else if (profiles.ValueKind == JsonValueKind.Array)
                {
                    foreach (var profile in profiles.EnumerateArray())
                        TryAddProfile(profile, sessions);
                }

                break; // Use first found settings file
            }
            catch { }
        }

        return sessions;
    }

    private static void TryAddProfile(JsonElement profile, List<SavedSession> sessions)
    {
        bool hidden = profile.TryGetProperty("hidden", out var h) && h.GetBoolean();
        if (hidden) return;

        string name = profile.TryGetProperty("name", out var n) ? n.GetString() ?? "Unknown" : "Unknown";
        string command = profile.TryGetProperty("commandline", out var c) ? c.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(command)) return;

        string workDir = profile.TryGetProperty("startingDirectory", out var d) ? d.GetString() ?? "" : "";
        workDir = workDir.Replace("%USERPROFILE%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

        sessions.Add(new SavedSession
        {
            Name = $"[WT] {name}",
            ShellPath = command,
            WorkingDirectory = workDir,
            ColorTag = "#00e5ff"
        });
    }

    private static string StripJsonComments(string json)
    {
        var sb = new System.Text.StringBuilder(json.Length);
        bool inString = false;
        bool escape = false;

        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];

            if (escape) { sb.Append(c); escape = false; continue; }
            if (c == '\\' && inString) { sb.Append(c); escape = true; continue; }
            if (c == '"') { inString = !inString; sb.Append(c); continue; }

            if (!inString)
            {
                if (c == '/' && i + 1 < json.Length)
                {
                    if (json[i + 1] == '/')
                    {
                        while (i < json.Length && json[i] != '\n') i++;
                        continue;
                    }
                    if (json[i + 1] == '*')
                    {
                        i += 2;
                        while (i + 1 < json.Length && !(json[i] == '*' && json[i + 1] == '/')) i++;
                        i++;
                        continue;
                    }
                }
            }

            sb.Append(c);
        }

        return sb.ToString();
    }
}
