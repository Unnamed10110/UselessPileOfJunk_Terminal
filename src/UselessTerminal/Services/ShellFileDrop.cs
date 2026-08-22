using System.IO;

namespace UselessTerminal.Services;

internal enum FileConflictChoice
{
    Cancel,
    Replace,
    Rename,
}

/// <summary>Copies Explorer-dropped files/folders into a shell working directory.</summary>
internal static class ShellFileDrop
{
    public static string? ResolveDestinationDirectory(string? cwd, string? fallback)
    {
        foreach (string? candidate in new[] { cwd, fallback })
        {
            string? resolved = TryResolveExistingDirectory(candidate);
            if (resolved is not null) return resolved;
        }
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Directory.Exists(home) ? home : null;
    }

    public static string SuggestRename(string fileName)
    {
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        return $"{stem} (1){ext}";
    }

    public static string ItemName(string path)
        => Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

    public static List<string> FindNameConflicts(IReadOnlyList<string> sources, string destDir)
    {
        var list = new List<string>();
        foreach (string src in sources)
        {
            string name = ItemName(src);
            if (string.IsNullOrEmpty(name)) continue;
            string dest = Path.Combine(destDir, name);
            try
            {
                if (string.Equals(Path.GetFullPath(src), Path.GetFullPath(dest), StringComparison.OrdinalIgnoreCase))
                    continue;
            }
            catch { }

            if (File.Exists(dest) || Directory.Exists(dest))
                list.Add(name);
        }

        return list;
    }

    public sealed class CopyResult
    {
        public List<string> CopiedNames { get; } = [];
        public List<string> Errors { get; } = [];
        public int Copied => CopiedNames.Count;
        public bool HasErrors => Errors.Count > 0;
        public bool HasCopied => CopiedNames.Count > 0;
    }

    public static CopyResult CopyDroppedPaths(
        IReadOnlyList<string> sources,
        string destDir,
        FileConflictChoice conflict = FileConflictChoice.Rename,
        Action<string>? progress = null)
    {
        var result = new CopyResult();
        if (!Directory.Exists(destDir))
        {
            result.Errors.Add($"Shell directory not found: {destDir}");
            return result;
        }

        int total = sources.Count;
        int i = 0;
        foreach (string src in sources)
        {
            i++;
            string label = ItemName(src);
            progress?.Invoke(total <= 1 ? $"Copying {label}…" : $"Copying {i} of {total}…\n{label}");
            try
            {
                string? destName = CopyOne(src, destDir, conflict);
                if (!string.IsNullOrEmpty(destName))
                    result.CopiedNames.Add(destName);
            }
            catch (Exception ex)
            {
                result.Errors.Add($"{label}: {ex.Message}");
            }
        }

        return result;
    }

    private static string? CopyOne(string src, string destDir, FileConflictChoice conflict)
    {
        string fullSrc = Path.GetFullPath(src);
        string name = ItemName(fullSrc);
        if (string.IsNullOrEmpty(name)) return null;

        string dest = Path.Combine(destDir, name);
        bool exists = File.Exists(dest) || Directory.Exists(dest);
        if (exists && conflict != FileConflictChoice.Replace)
            dest = UniquePath(destDir, name);
        else if (exists)
            DeleteExisting(dest);

        string destFull = Path.GetFullPath(dest);

        if (string.Equals(fullSrc, destFull, StringComparison.OrdinalIgnoreCase))
            return name;

        if (File.Exists(fullSrc))
        {
            File.Copy(fullSrc, destFull, overwrite: true);
            return Path.GetFileName(destFull);
        }

        if (!Directory.Exists(fullSrc))
            throw new FileNotFoundException("Path not found.", fullSrc);

        string srcPrefix = fullSrc.TrimEnd('\\') + '\\';
        string destDirFull = Path.GetFullPath(destDir).TrimEnd('\\') + '\\';
        if (destDirFull.StartsWith(srcPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Cannot copy a folder into itself.");

        CopyDirectory(fullSrc, destFull);
        return Path.GetFileName(destFull);
    }

    private static void DeleteExisting(string dest)
    {
        if (File.Exists(dest))
        {
            File.SetAttributes(dest, FileAttributes.Normal);
            File.Delete(dest);
            return;
        }

        if (Directory.Exists(dest))
            Directory.Delete(dest, recursive: true);
    }

    private static void CopyDirectory(string src, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (string file in Directory.GetFiles(src))
            File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
        foreach (string dir in Directory.GetDirectories(src))
            CopyDirectory(dir, Path.Combine(dest, Path.GetFileName(dir)));
    }

    private static string UniquePath(string destDir, string fileName)
    {
        string dest = Path.Combine(destDir, fileName);
        if (!File.Exists(dest) && !Directory.Exists(dest)) return dest;

        string stem = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        for (int i = 1; i < 1000; i++)
        {
            string candidate = Path.Combine(destDir, $"{stem} ({i}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
        }

        return Path.Combine(destDir, $"{stem} ({Guid.NewGuid():N}){ext}");
    }

    internal static string? TryResolveExistingDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = path.Trim().Trim('"');

        if (Directory.Exists(path))
            return Path.GetFullPath(path);

        string? converted = ConvertUnixStylePath(path);
        if (converted is not null && Directory.Exists(converted))
            return Path.GetFullPath(converted);

        return null;
    }

    private static string? ConvertUnixStylePath(string path)
    {
        path = path.Replace('\\', '/');
        if (path.Length >= 7 && path.StartsWith("/mnt/", StringComparison.OrdinalIgnoreCase)
            && char.IsLetter(path[5]) && path[6] == '/')
        {
            return char.ToUpperInvariant(path[5]) + ":" + path[6..].Replace('/', '\\');
        }

        if (path.Length >= 3 && path[0] == '/' && char.IsLetter(path[1]) && path[2] == '/')
            return char.ToUpperInvariant(path[1]) + ":" + path[2..].Replace('/', '\\');

        if (path.Length >= 3 && char.IsLetter(path[0]) && path[1] == ':' && path[2] == '/')
            return path.Replace('/', '\\');

        return null;
    }
}
