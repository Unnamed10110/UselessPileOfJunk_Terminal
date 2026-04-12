using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;

namespace UselessTerminal.Services;

/// <summary>
/// Records terminal output in asciicast v2 format.
/// https://docs.asciinema.org/manual/asciicast/v2/
/// </summary>
public sealed class AsciicastRecorder : IDisposable
{
    private StreamWriter? _writer;
    private readonly Stopwatch _stopwatch = new();
    private bool _disposed;

    public bool IsRecording => _writer is not null;
    public string? FilePath { get; private set; }

    private static readonly string RecordingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UselessTerminal", "recordings");

    public void Start(string title, int cols = 120, int rows = 30)
    {
        if (_writer is not null) return;
        try
        {
            if (!Directory.Exists(RecordingsDir)) Directory.CreateDirectory(RecordingsDir);
            string safe = SanitizeFileName(title);
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            FilePath = Path.Combine(RecordingsDir, $"{safe}-{stamp}.cast");

            _writer = new StreamWriter(FilePath, false, new UTF8Encoding(false)) { AutoFlush = true };

            var header = new Dictionary<string, object>
            {
                ["version"] = 2,
                ["width"] = cols,
                ["height"] = rows,
                ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["title"] = title,
                ["env"] = new Dictionary<string, string> { ["TERM"] = "xterm-256color", ["SHELL"] = "ConPTY" }
            };
            _writer.WriteLine(JsonSerializer.Serialize(header));
            _stopwatch.Restart();
        }
        catch
        {
            _writer = null;
            FilePath = null;
        }
    }

    public void WriteOutput(byte[] data)
    {
        if (_writer is null) return;
        try
        {
            double elapsed = _stopwatch.Elapsed.TotalSeconds;
            string text = Encoding.UTF8.GetString(data);
            string escaped = JsonSerializer.Serialize(text);
            _writer.WriteLine($"[{elapsed:F6}, \"o\", {escaped}]");
        }
        catch { }
    }

    public void WriteInput(string data)
    {
        if (_writer is null) return;
        try
        {
            double elapsed = _stopwatch.Elapsed.TotalSeconds;
            string escaped = JsonSerializer.Serialize(data);
            _writer.WriteLine($"[{elapsed:F6}, \"i\", {escaped}]");
        }
        catch { }
    }

    public void Stop()
    {
        if (_writer is null) return;
        _stopwatch.Stop();
        try { _writer.Dispose(); } catch { }
        _writer = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (char c in name)
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        string result = sb.ToString().Trim();
        return string.IsNullOrWhiteSpace(result) ? "recording" : result;
    }
}
