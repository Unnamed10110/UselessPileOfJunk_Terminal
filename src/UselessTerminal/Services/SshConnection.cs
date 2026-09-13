using System.IO;
using System.Text;

namespace UselessTerminal.Services;

/// <summary>Parsed OpenSSH connection used to copy dropped files with scp.</summary>
internal sealed record SshConnection
{
    private static readonly HashSet<string> OptionsWithValue = new(StringComparer.Ordinal)
    {
        "-B", "-b", "-c", "-D", "-E", "-e", "-F", "-I", "-i", "-J", "-L", "-l",
        "-m", "-O", "-o", "-p", "-Q", "-R", "-S", "-W", "-w",
    };

    public required string SshExe { get; init; }
    public string? ScpExe { get; init; }
    public string? User { get; init; }
    public required string Host { get; init; }
    public int? Port { get; init; }
    public string? IdentityFile { get; init; }
    public string? ConfigFile { get; init; }
    public string? JumpHost { get; init; }
    public string? ControlPath { get; init; }
    public bool IsPrimaryShell { get; init; }
    public List<string> ExtraDashO { get; init; } = [];

    public string DisplayTarget => string.IsNullOrEmpty(User) ? Host : $"{User}@{Host}";

    public static bool LooksLikeSsh(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        string exe = ShellGlyphResolver.ParseExecutable(command);
        return IsSshExecutable(exe);
    }

    public static bool IsSshExecutable(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        string name = Path.GetFileNameWithoutExtension(path.Trim().Trim('"'));
        return name.Equals("ssh", StringComparison.OrdinalIgnoreCase);
    }

    public static string DefaultMuxControlPath()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UselessTerminal", "ssh-mux");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "%C").Replace('\\', '/');
    }

    /// <summary>
    /// Adds ControlMaster so a later scp can reuse this login (including password auth).
    /// </summary>
    public static string InjectControlMaster(string command, out string? controlPath)
    {
        controlPath = null;
        var parsed = TryParse(command, null);
        if (parsed is null) return command;

        if (!string.IsNullOrWhiteSpace(parsed.ControlPath))
        {
            controlPath = parsed.ControlPath;
            return command;
        }

        controlPath = DefaultMuxControlPath();
        string args = StripExecutable(command);
        string exeToken = ExecutableToken(command);
        string pathArg = controlPath.Contains(' ') ? $"\"{controlPath}\"" : controlPath;
        return $"{exeToken} -o ControlMaster=auto -o ControlPersist=yes -o ControlPath={pathArg} {args}".TrimEnd();
    }

    public static SshConnection? Resolve(string? shellCommand, int sessionPid, string? injectedControlPath, string? fallbackHost = null)
    {
        var fromShell = TryParse(shellCommand ?? "", injectedControlPath);
        if (fromShell is not null)
            return fromShell with { IsPrimaryShell = true, ControlPath = injectedControlPath ?? fromShell.ControlPath };

        if (sessionPid <= 0) return null;
        foreach (var proc in ProcessCommandLines.GetDescendants(sessionPid))
        {
            if (!IsSshExecutable(proc.ExePath) && !IsSshExecutable(proc.ExeName))
                continue;
            string cmd = proc.CommandLine ?? proc.ExePath ?? proc.ExeName;
            var parsed = TryParse(cmd, null);
            if (parsed is not null) return parsed;

            string sshExe = proc.ExePath ?? proc.ExeName;
            if (string.IsNullOrWhiteSpace(fallbackHost) || IsLocalHostName(fallbackHost))
                continue;
            return new SshConnection
            {
                SshExe = sshExe,
                ScpExe = SiblingScp(sshExe),
                Host = fallbackHost,
            };
        }

        return null;
    }

    public static bool IsLocalHostName(string? host)
    {
        if (string.IsNullOrWhiteSpace(host)) return true;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)) return true;
        if (host.Equals("::1", StringComparison.OrdinalIgnoreCase)) return true;
        return host.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);
    }

    public static SshConnection? TryParse(string commandLine, string? injectedControlPath)
    {
        var argv = SplitArgs(commandLine);
        if (argv.Count == 0) return null;
        if (!IsSshExecutable(argv[0])) return null;

        string sshExe = argv[0];
        string? user = null;
        string? host = null;
        int? port = null;
        string? identity = null;
        string? config = null;
        string? jump = null;
        string? controlPath = injectedControlPath;
        var extraO = new List<string>();

        for (int i = 1; i < argv.Count; i++)
        {
            string tok = argv[i];
            if (tok == "--")
            {
                if (i + 1 < argv.Count && host is null)
                    ParseDestination(argv[i + 1], ref user, ref host);
                break;
            }

            if (tok.Length >= 2 && tok[0] == '-' && tok[1] != '-')
            {
                string flag = tok.Length == 2 || tok[1] == 'o' || tok[1] == 'p' || tok[1] == 'i' || tok[1] == 'l' || tok[1] == 'F' || tok[1] == 'J'
                    ? tok[..Math.Min(2, tok.Length)]
                    : tok;
                string? bundled = tok.Length > 2 && flag.Length == 2 ? tok[2..] : null;

                if (flag == "-p")
                {
                    string? val = bundled ?? Next(argv, ref i);
                    if (int.TryParse(val, out int p)) port = p;
                    continue;
                }
                if (flag == "-l")
                {
                    user = bundled ?? Next(argv, ref i);
                    continue;
                }
                if (flag == "-i")
                {
                    identity = ExpandUserPath(bundled ?? Next(argv, ref i));
                    continue;
                }
                if (flag == "-F")
                {
                    config = ExpandUserPath(bundled ?? Next(argv, ref i));
                    continue;
                }
                if (flag == "-J")
                {
                    jump = bundled ?? Next(argv, ref i);
                    continue;
                }
                if (flag == "-o")
                {
                    string opt = bundled ?? Next(argv, ref i) ?? "";
                    if (opt.StartsWith("ControlPath=", StringComparison.OrdinalIgnoreCase))
                        controlPath = opt["ControlPath=".Length..].Trim().Trim('"');
                    else if (!opt.StartsWith("ControlMaster=", StringComparison.OrdinalIgnoreCase)
                             && !opt.StartsWith("ControlPersist=", StringComparison.OrdinalIgnoreCase)
                             && !opt.StartsWith("BatchMode=", StringComparison.OrdinalIgnoreCase))
                        extraO.Add(opt);
                    continue;
                }

                if (OptionsWithValue.Contains(flag) && bundled is null)
                    i++;
                continue;
            }

            if (host is null)
            {
                ParseDestination(tok, ref user, ref host);
                continue;
            }

            // Remote command — ignore the rest.
            break;
        }

        if (string.IsNullOrWhiteSpace(host)) return null;

        return new SshConnection
        {
            SshExe = sshExe,
            ScpExe = SiblingScp(sshExe),
            User = user,
            Host = host,
            Port = port,
            IdentityFile = identity,
            ConfigFile = config,
            JumpHost = jump,
            ControlPath = controlPath,
            ExtraDashO = extraO,
        };
    }

    public void AddConnectionArguments(IList<string> args, bool scp)
    {
        args.Add("-o");
        args.Add("BatchMode=yes");
        if (!string.IsNullOrWhiteSpace(ControlPath))
        {
            args.Add("-o");
            args.Add("ControlMaster=auto");
            args.Add("-o");
            args.Add("ControlPersist=yes");
            args.Add("-o");
            args.Add("ControlPath=" + ControlPath);
        }
        if (Port is int port)
        {
            args.Add(scp ? "-P" : "-p");
            args.Add(port.ToString());
        }
        if (!string.IsNullOrWhiteSpace(IdentityFile))
        {
            args.Add("-i");
            args.Add(IdentityFile);
        }
        if (!string.IsNullOrWhiteSpace(ConfigFile))
        {
            args.Add("-F");
            args.Add(ConfigFile);
        }
        if (!string.IsNullOrWhiteSpace(JumpHost))
        {
            args.Add("-J");
            args.Add(JumpHost);
        }
        foreach (string opt in ExtraDashO)
        {
            args.Add("-o");
            args.Add(opt);
        }
    }

    public string RemoteTarget(string remotePath)
        => $"{DisplayTarget}:{remotePath}";

    private static void ParseDestination(string token, ref string? user, ref string? host)
    {
        token = token.Trim();
        int at = token.LastIndexOf('@');
        if (at > 0)
        {
            user ??= token[..at];
            host = token[(at + 1)..];
        }
        else
        {
            host = token;
        }
    }

    private static string? Next(List<string> argv, ref int i)
        => ++i < argv.Count ? argv[i] : null;

    private static string? SiblingScp(string sshExe)
    {
        try
        {
            string? dir = Path.GetDirectoryName(sshExe.Trim('"'));
            if (string.IsNullOrEmpty(dir)) return FindOnPath("scp.exe");
            string scp = Path.Combine(dir, "scp.exe");
            return File.Exists(scp) ? scp : FindOnPath("scp.exe");
        }
        catch
        {
            return FindOnPath("scp.exe");
        }
    }

    private static string? FindOnPath(string fileName)
    {
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv is null) return null;
        foreach (string dir in pathEnv.Split(';'))
        {
            string full = Path.Combine(dir.Trim(), fileName);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    private static string? ExpandUserPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        path = path.Trim('"');
        if (path.StartsWith("~") )
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = path.Length == 1 || path[1] is '/' or '\\'
                ? home + path[1..]
                : path;
        }
        return path;
    }

    private static string ExecutableToken(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            return end >= 0 ? command[..(end + 1)] : $"\"{command.Trim('"')}\"";
        }

        int sp = command.IndexOfAny([' ', '\t']);
        string exe = sp < 0 ? command : command[..sp];
        return exe.Contains('\\') || exe.Contains(' ') ? $"\"{exe.Trim('"')}\"" : exe;
    }

    private static string StripExecutable(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            return end >= 0 ? command[(end + 1)..].TrimStart() : "";
        }
        int sp = command.IndexOfAny([' ', '\t']);
        return sp < 0 ? "" : command[(sp + 1)..].TrimStart();
    }

    internal static List<string> SplitArgs(string commandLine)
    {
        var list = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        foreach (char c in commandLine)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (sb.Length > 0)
                {
                    list.Add(sb.ToString());
                    sb.Clear();
                }
                continue;
            }
            sb.Append(c);
        }
        if (sb.Length > 0) list.Add(sb.ToString());
        return list;
    }
}
