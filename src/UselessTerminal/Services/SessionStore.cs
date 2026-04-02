using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
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
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public List<SessionFolder> Folders { get; private set; } = new();
    public List<SavedSession> Sessions { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (!File.Exists(StorePath))
                return;

            string json = File.ReadAllText(StorePath).TrimStart();
            if (json.StartsWith('['))
            {
                Sessions = JsonSerializer.Deserialize<List<SavedSession>>(json, JsonOptions) ?? new();
                Folders = new();
                foreach (var s in Sessions)
                    s.FolderId = NormalizeFolderId(s.FolderId);
                Save();
                return;
            }

            var root = JsonSerializer.Deserialize<SessionStoreRoot>(json, JsonOptions);
            if (root is null)
            {
                Sessions = new();
                Folders = new();
                return;
            }

            Folders = root.Folders ?? new();
            Sessions = root.Sessions ?? new();
            foreach (var s in Sessions)
                s.FolderId = NormalizeFolderId(s.FolderId);
            FlattenFoldersToRoot();
        }
        catch
        {
            Sessions = new();
            Folders = new();
        }
    }

    private static string? NormalizeFolderId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return id;
    }

    public void Save()
    {
        try
        {
            string? dir = Path.GetDirectoryName(StorePath);
            if (dir is not null)
                Directory.CreateDirectory(dir);

            var root = new SessionStoreRoot { Folders = Folders, Sessions = Sessions };
            string json = JsonSerializer.Serialize(root, JsonOptions);
            File.WriteAllText(StorePath, json);
        }
        catch { }
    }

    public void Add(SavedSession session)
    {
        session.FolderId = NormalizeFolderId(session.FolderId);
        session.SortOrder = NextSessionSortOrder(session.FolderId);
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

    public SessionFolder? FindFolderById(string id) => Folders.FirstOrDefault(f => f.Id == id);

    public void AddFolder(SessionFolder folder)
    {
        folder.ParentId = null;
        folder.SortOrder = NextFolderSortOrder(null);
        Folders.Add(folder);
        Save();
    }

    public void UpdateFolder(SessionFolder folder)
    {
        folder.ParentId = null;
        int i = Folders.FindIndex(f => f.Id == folder.Id);
        if (i >= 0)
        {
            Folders[i] = folder;
            Save();
        }
    }

    public void RemoveFolder(SessionFolder folder)
    {
        foreach (var s in Sessions.Where(s => s.FolderId == folder.Id))
            s.FolderId = null;

        NormalizeSessionOrders(null);
        Folders.RemoveAll(f => f.Id == folder.Id);
        NormalizeFolderOrders(null);
        Save();
    }

    /// <summary>Folders are root-only (no nesting). Re-append at end of root folder list.</summary>
    public void MoveFolderToRoot(SessionFolder folder)
    {
        string? oldParent = NormalizeFolderId(folder.ParentId);
        folder.ParentId = null;
        folder.SortOrder = NextFolderSortOrder(null);
        NormalizeFolderOrders(oldParent);
        NormalizeFolderOrders(null);
        Save();
    }

    public void MoveFolderBeforeSibling(SessionFolder moving, SessionFolder anchor)
    {
        if (moving.Id == anchor.Id) return;
        string? oldParent = NormalizeFolderId(moving.ParentId);
        moving.ParentId = null;
        anchor.ParentId = null;
        var siblings = Folders.Where(f => f.ParentId == null && f.Id != moving.Id)
            .OrderBy(f => f.SortOrder).ToList();
        int insert = siblings.FindIndex(f => f.Id == anchor.Id);
        if (insert < 0) insert = siblings.Count;
        siblings.Insert(insert, moving);
        for (int i = 0; i < siblings.Count; i++)
            siblings[i].SortOrder = i;
        NormalizeFolderOrders(oldParent);
        NormalizeFolderOrders(null);
        Save();
    }

    public void MoveFolderAfterSibling(SessionFolder moving, SessionFolder anchor)
    {
        if (moving.Id == anchor.Id) return;
        string? oldParent = NormalizeFolderId(moving.ParentId);
        moving.ParentId = null;
        anchor.ParentId = null;
        var siblings = Folders.Where(f => f.ParentId == null && f.Id != moving.Id)
            .OrderBy(f => f.SortOrder).ToList();
        int insert = siblings.FindIndex(f => f.Id == anchor.Id);
        if (insert < 0) insert = siblings.Count - 1;
        else insert++;
        siblings.Insert(insert, moving);
        for (int i = 0; i < siblings.Count; i++)
            siblings[i].SortOrder = i;
        NormalizeFolderOrders(oldParent);
        NormalizeFolderOrders(null);
        Save();
    }

    private void FlattenFoldersToRoot()
    {
        bool changed = false;
        foreach (var f in Folders)
        {
            if (!string.IsNullOrEmpty(f.ParentId))
            {
                f.ParentId = null;
                changed = true;
            }
        }

        if (changed)
        {
            NormalizeFolderOrders(null);
            Save();
        }
    }

    public void MoveSessionToFolder(SavedSession session, string? folderId)
    {
        string? old = NormalizeFolderId(session.FolderId);
        folderId = NormalizeFolderId(folderId);
        session.FolderId = folderId;
        session.SortOrder = NextSessionSortOrder(folderId);
        NormalizeSessionOrders(old);
        NormalizeSessionOrders(folderId);
        Save();
    }

    public void MoveSessionBeforeSibling(SavedSession moving, SavedSession anchor)
    {
        if (moving.Id == anchor.Id) return;
        string? oldFid = NormalizeFolderId(moving.FolderId);
        string? fid = NormalizeFolderId(anchor.FolderId);
        moving.FolderId = fid;
        var list = Sessions.Where(s => s.FolderId == fid && s.Id != moving.Id).OrderBy(s => s.SortOrder).ToList();
        int insert = list.FindIndex(s => s.Id == anchor.Id);
        if (insert < 0) insert = list.Count;
        list.Insert(insert, moving);
        for (int i = 0; i < list.Count; i++)
            list[i].SortOrder = i;
        NormalizeSessionOrders(oldFid);
        Save();
    }

    public void MoveSessionAfterSibling(SavedSession moving, SavedSession anchor)
    {
        if (moving.Id == anchor.Id) return;
        string? oldFid = NormalizeFolderId(moving.FolderId);
        string? fid = NormalizeFolderId(anchor.FolderId);
        moving.FolderId = fid;
        var list = Sessions.Where(s => s.FolderId == fid && s.Id != moving.Id).OrderBy(s => s.SortOrder).ToList();
        int insert = list.FindIndex(s => s.Id == anchor.Id);
        if (insert < 0) insert = list.Count - 1;
        else insert++;
        list.Insert(insert, moving);
        for (int i = 0; i < list.Count; i++)
            list[i].SortOrder = i;
        NormalizeSessionOrders(oldFid);
        Save();
    }

    private void NormalizeFolderOrders(string? parentId)
    {
        var list = Folders.Where(f => f.ParentId == parentId).OrderBy(f => f.SortOrder).ToList();
        for (int i = 0; i < list.Count; i++)
            list[i].SortOrder = i;
    }

    private void NormalizeSessionOrders(string? folderId)
    {
        var list = Sessions.Where(s => s.FolderId == folderId).OrderBy(s => s.SortOrder).ToList();
        for (int i = 0; i < list.Count; i++)
            list[i].SortOrder = i;
    }

    private int NextFolderSortOrder(string? parentId)
    {
        var list = Folders.Where(f => f.ParentId == parentId).Select(f => f.SortOrder);
        return list.Any() ? list.Max() + 1 : 0;
    }

    private int NextSessionSortOrder(string? folderId)
    {
        var list = Sessions.Where(s => s.FolderId == folderId).Select(s => s.SortOrder);
        return list.Any() ? list.Max() + 1 : 0;
    }

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

        for (int i = 0; i < Sessions.Count; i++)
            Sessions[i].SortOrder = i;
        Save();
    }

    public void ExportToFile(string path)
    {
        var root = new SessionStoreRoot { Folders = Folders, Sessions = Sessions };
        string json = JsonSerializer.Serialize(root, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>Replaces folders and sessions from a JSON file (same format as the app store or legacy session array).</summary>
    public void ImportFromFile(string path)
    {
        string json = File.ReadAllText(path).TrimStart();
        if (json.StartsWith('['))
        {
            Sessions = JsonSerializer.Deserialize<List<SavedSession>>(json, JsonOptions) ?? new();
            Folders = new();
            foreach (var s in Sessions)
                s.FolderId = NormalizeFolderId(s.FolderId);
        }
        else
        {
            var root = JsonSerializer.Deserialize<SessionStoreRoot>(json, JsonOptions);
            if (root is null)
            {
                Sessions = new();
                Folders = new();
            }
            else
            {
                Folders = root.Folders ?? new();
                Sessions = root.Sessions ?? new();
                foreach (var s in Sessions)
                    s.FolderId = NormalizeFolderId(s.FolderId);
            }
        }

        FlattenFoldersToRoot();
        Save();
    }
}

internal sealed class SessionStoreRoot
{
    public int Version { get; set; } = 2;
    public List<SessionFolder> Folders { get; set; } = new();
    public List<SavedSession> Sessions { get; set; } = new();
}
