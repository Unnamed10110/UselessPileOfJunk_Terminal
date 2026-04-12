using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using UselessTerminal.Models;

namespace UselessTerminal.Services;

public sealed class WorkspaceStore
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UselessTerminal", "workspaces.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static WorkspaceStore Instance { get; } = new();

    public List<WorkspaceProfile> Workspaces { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (!File.Exists(StorePath)) return;
            string json = File.ReadAllText(StorePath);
            Workspaces = JsonSerializer.Deserialize<List<WorkspaceProfile>>(json, JsonOptions) ?? new();
        }
        catch
        {
            Workspaces = new();
        }
    }

    public void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(StorePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(Workspaces, JsonOptions));
        }
        catch { }
    }

    public void Add(WorkspaceProfile profile)
    {
        Workspaces.Add(profile);
        Save();
    }

    public void Remove(WorkspaceProfile profile)
    {
        Workspaces.Remove(profile);
        Save();
    }

    public void Update()
    {
        Save();
    }

    public WorkspaceProfile? FindByName(string name)
        => Workspaces.FirstOrDefault(w => w.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
}
