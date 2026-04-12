using System.Windows.Threading;

namespace UselessTerminal.Services;

/// <summary>
/// Heuristic command completion detector. When a tab produces output while the
/// window is inactive and then goes quiet for <see cref="QuietThresholdMs"/>,
/// a notification is raised.
/// </summary>
public sealed class CommandNotifier : IDisposable
{
    private const int QuietThresholdMs = 2500;
    private const int MinOutputBursts = 3;

    private readonly DispatcherTimer _timer;
    private int _burstCount;
    private bool _armed;
    private string _tabTitle = "";
    private bool _disposed;

    public bool Enabled { get; set; } = true;
    public bool WindowIsActive { get; set; } = true;

    public event Action<string>? CommandFinished;

    public CommandNotifier()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(QuietThresholdMs) };
        _timer.Tick += OnQuietPeriodElapsed;
    }

    public void OnOutputReceived(string tabTitle)
    {
        if (!Enabled || WindowIsActive || _disposed) return;

        _tabTitle = tabTitle;
        _burstCount++;
        _armed = _burstCount >= MinOutputBursts;
        _timer.Stop();
        _timer.Start();
    }

    private void OnQuietPeriodElapsed(object? sender, EventArgs e)
    {
        _timer.Stop();
        if (_armed && !WindowIsActive)
        {
            CommandFinished?.Invoke(_tabTitle);
        }
        _burstCount = 0;
        _armed = false;
    }

    public void Reset()
    {
        _timer.Stop();
        _burstCount = 0;
        _armed = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
    }
}
