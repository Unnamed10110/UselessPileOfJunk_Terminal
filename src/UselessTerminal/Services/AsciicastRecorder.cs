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
    private const int FlushThresholdBytes = 32 * 1024;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(1000);

    private readonly object _sync = new();
    private StreamWriter? _writer;
    private System.Threading.Timer? _flushTimer;
    private int _unflushedBytes;
    private readonly Stopwatch _stopwatch = new();
    private bool _disposed;

    public bool IsRecording => _writer is not null;
    public string? FilePath { get; private set; }

    private static readonly string RecordingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UselessTerminal", "recordings");

    public void Start(string title, int cols = 120, int rows = 30)
    {
        lock (_sync)
        {
            if (_writer is not null) return;
            try
            {
                if (!Directory.Exists(RecordingsDir)) Directory.CreateDirectory(RecordingsDir);
                string safe = SanitizeFileName(title);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                FilePath = Path.Combine(RecordingsDir, $"{safe}-{stamp}.cast");

                var stream = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.Read, bufferSize: 64 * 1024);
                _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = false };
                _unflushedBytes = 0;

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
                _flushTimer = new System.Threading.Timer(_ => FlushOnTimer(), null, FlushInterval, FlushInterval);
            }
            catch
            {
                _writer = null;
                FilePath = null;
            }
        }
    }

    private void FlushOnTimer()
    {
        lock (_sync)
        {
            try
            {
                _writer?.Flush();
                _unflushedBytes = 0;
            }
            catch { }
        }
    }

    public void WriteOutput(byte[] data)
    {
        lock (_sync)
        {
            if (_writer is null) return;
            try
            {
                double elapsed = _stopwatch.Elapsed.TotalSeconds;
                string text = Encoding.UTF8.GetString(data);
                string escaped = JsonSerializer.Serialize(text);
                _writer.WriteLine($"[{elapsed:F6}, \"o\", {escaped}]");
                _unflushedBytes += data.Length;
                if (_unflushedBytes >= FlushThresholdBytes)
                {
                    _writer.Flush();
                    _unflushedBytes = 0;
                }
            }
            catch { }
        }
    }

    public void WriteInput(string data)
    {
        lock (_sync)
        {
            if (_writer is null) return;
            try
            {
                double elapsed = _stopwatch.Elapsed.TotalSeconds;
                string escaped = JsonSerializer.Serialize(data);
                _writer.WriteLine($"[{elapsed:F6}, \"i\", {escaped}]");
                _unflushedBytes += Encoding.UTF8.GetByteCount(data);
                if (_unflushedBytes >= FlushThresholdBytes)
                {
                    _writer.Flush();
                    _unflushedBytes = 0;
                }
            }
            catch { }
        }
    }

    public void Stop()
    {
        lock (_sync)
        {
            if (_writer is null) return;
            _stopwatch.Stop();
            _flushTimer?.Dispose();
            _flushTimer = null;
            try
            {
                _writer.Flush();
                _writer.Dispose();
            }
            catch { }
            _writer = null;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
        }
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
