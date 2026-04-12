using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace UselessTerminal.Services;

public sealed partial class TerminalLogger : IDisposable
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UselessTerminal", "logs");

    private StreamWriter? _writer;
    private bool _disposed;

    public bool IsLogging => _writer is not null;
    public string? LogFilePath { get; private set; }

    public void Start(string tabTitle)
    {
        if (_writer is not null) return;
        try
        {
            if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);
            string safe = SanitizeFileName(tabTitle);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            LogFilePath = Path.Combine(LogDir, $"{safe}-{stamp}.log");
            _writer = new StreamWriter(LogFilePath, append: true, Encoding.UTF8) { AutoFlush = true };
            _writer.WriteLine($"--- Session log started: {DateTime.Now:O} ---");
        }
        catch
        {
            _writer = null;
            LogFilePath = null;
        }
    }

    public void Stop()
    {
        if (_writer is null) return;
        try
        {
            _writer.WriteLine($"--- Session log ended: {DateTime.Now:O} ---");
            _writer.Dispose();
        }
        catch { }
        _writer = null;
    }

    public void Write(byte[] data)
    {
        if (_writer is null) return;
        try
        {
            string raw = Encoding.UTF8.GetString(data);
            string clean = StripAnsi(raw);
            _writer.Write(clean);
        }
        catch { }
    }

    private static string StripAnsi(string text)
    {
        return AnsiPattern().Replace(text, "");
    }

    [GeneratedRegex(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~]|\][^\x07\x1B]*(?:\x07|\x1B\\))")]
    private static partial Regex AnsiPattern();

    private static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        string result = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? "session" : result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }
}
