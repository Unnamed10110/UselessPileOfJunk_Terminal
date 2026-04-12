using System.IO;
using System.Text.Json;
using UselessTerminal.Models;

namespace UselessTerminal.Services;

public sealed class SnippetStore
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UselessTerminal", "snippets.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static SnippetStore Instance { get; } = new();

    public List<Snippet> Snippets { get; private set; } = new();

    public event Action? SnippetsChanged;

    private SnippetStore() { }

    public void Load()
    {
        try
        {
            if (File.Exists(StorePath))
            {
                string json = File.ReadAllText(StorePath);
                Snippets = JsonSerializer.Deserialize<List<Snippet>>(json, JsonOptions) ?? new();
            }
        }
        catch
        {
            Snippets = new();
        }
    }

    public void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(StorePath);
            if (dir is not null) Directory.CreateDirectory(dir);
            string json = JsonSerializer.Serialize(Snippets, JsonOptions);
            File.WriteAllText(StorePath, json);
        }
        catch { }
    }

    public void Add(Snippet snippet)
    {
        snippet.SortOrder = Snippets.Count;
        Snippets.Add(snippet);
        Save();
        SnippetsChanged?.Invoke();
    }

    public void Remove(Snippet snippet)
    {
        Snippets.Remove(snippet);
        Save();
        SnippetsChanged?.Invoke();
    }

    public void Update(Snippet snippet)
    {
        int i = Snippets.FindIndex(s => s.Id == snippet.Id);
        if (i >= 0)
        {
            Snippets[i] = snippet;
            Save();
            SnippetsChanged?.Invoke();
        }
    }
}
