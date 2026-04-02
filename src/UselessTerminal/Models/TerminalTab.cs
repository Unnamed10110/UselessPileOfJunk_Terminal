using UselessTerminal.Controls;
using UselessTerminal.Services;

namespace UselessTerminal.Models;

public sealed class TerminalTab : IDisposable
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Terminal";
    public TerminalControl TerminalControl { get; }
    public string ShellCommand { get; }
    public string? WorkingDirectory { get; }

    public TerminalTab(string shellCommand, string? workingDirectory = null)
    {
        ShellCommand = shellCommand;
        WorkingDirectory = workingDirectory;
        TerminalControl = new TerminalControl();
        TerminalControl.TitleChanged += title => Title = title;
    }

    public void Start()
    {
        TerminalControl.StartSession(ShellCommand, WorkingDirectory);
    }

    public void Dispose()
    {
        TerminalControl.Dispose();
    }
}
