using System.IO;
using System.Text.Json;
using UselessTerminal.Models;
using Path = System.IO.Path;

namespace UselessTerminal.Services;

public sealed class SessionStore
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UselessTerminal", "sessions.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public List<SavedSession> Sessions { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(StorePath))
            {
                string json = File.ReadAllText(StorePath);
                Sessions = JsonSerializer.Deserialize<List<SavedSession>>(json, JsonOptions) ?? new();
            }
        }
        catch
        {
            Sessions = new();
        }
    }

    public void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(StorePath);
            if (dir is not null)
                Directory.CreateDirectory(dir);

            string json = JsonSerializer.Serialize(Sessions, JsonOptions);
            File.WriteAllText(StorePath, json);
        }
        catch { }
    }

    public void Add(SavedSession session)
    {
        session.SortOrder = Sessions.Count;
        Sessions.Add(session);
        Save();
    }

    public void Remove(SavedSession session)
    {
        Sessions.Remove(session);
        Save();
    }

    public void Update(SavedSession session)
    {
        int index = Sessions.FindIndex(s => s.Id == session.Id);
        if (index >= 0)
        {
            Sessions[index] = session;
            Save();
        }
    }

    public SavedSession? FindById(string id) => Sessions.FirstOrDefault(s => s.Id == id);

    public void SeedDefaults()
    {
        if (Sessions.Count > 0) return;

        var shells = ShellDetector.DetectShells();
        foreach (var shell in shells)
        {
            Sessions.Add(new SavedSession
            {
                Name = shell.Name,
                ShellPath = shell.Command,
                Arguments = shell.Arguments,
                IconGlyph = shell.IconGlyph
            });
        }
        Save();
    }
}
