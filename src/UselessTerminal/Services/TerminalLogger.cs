using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace UselessTerminal.Services;

public sealed partial class TerminalLogger : IDisposable
{
    private const int FlushThresholdBytes = 32 * 1024;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(1000);

    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UselessTerminal", "logs");

    private readonly object _sync = new();
    private StreamWriter? _writer;
    private System.Threading.Timer? _flushTimer;
    private int _unflushedBytes;
    private bool _disposed;

    public bool IsLogging => _writer is not null;
    public string? LogFilePath { get; private set; }

    public void Start(string tabTitle)
    {
        lock (_sync)
        {
            if (_writer is not null) return;
            try
            {
                if (!Directory.Exists(LogDir)) Directory.CreateDirectory(LogDir);
                string safe = SanitizeFileName(tabTitle);
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                LogFilePath = Path.Combine(LogDir, $"{safe}-{stamp}.log");
                var stream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read, bufferSize: 64 * 1024);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = false };
                _unflushedBytes = 0;
                _writer.WriteLine($"--- Session log started: {DateTime.Now:O} ---");
                _flushTimer = new System.Threading.Timer(_ => FlushOnTimer(), null, FlushInterval, FlushInterval);
            }
            catch
            {
                _writer = null;
                LogFilePath = null;
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

    public void Stop()
    {
        lock (_sync)
        {
            if (_writer is null) return;
            _flushTimer?.Dispose();
            _flushTimer = null;
            try
            {
                _writer.WriteLine($"--- Session log ended: {DateTime.Now:O} ---");
                _writer.Flush();
                _writer.Dispose();
            }
            catch { }
            _writer = null;
        }
    }

    public void Write(byte[] data)
    {
        lock (_sync)
        {
            if (_writer is null) return;
            try
            {
                string raw = Encoding.UTF8.GetString(data);
                string clean = StripAnsi(raw);
                _writer.Write(clean);
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
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
        }
        Stop();
    }
}
