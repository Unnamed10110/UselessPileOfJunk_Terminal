namespace UselessTerminal.Services;

/// <summary>
/// Parses the executable token from a full shell command line (quoted paths, arguments).
/// </summary>
public static class ShellGlyphResolver
{
    /// <summary>First token of a command line (handles quoted paths).</summary>
    public static string ParseExecutable(string command)
    {
        command = command.Trim();
        if (string.IsNullOrEmpty(command)) return "";
        if (command[0] == '"')
        {
            int end = command.IndexOf('"', 1);
            return end > 1 ? command.Substring(1, end - 1) : command;
        }

        int sp = command.IndexOfAny([' ', '\t']);
        return sp < 0 ? command : command[..sp];
    }
}
