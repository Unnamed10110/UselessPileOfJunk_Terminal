namespace UselessTerminal.Models;

public sealed class WindowState
{
    public double Left { get; set; } = double.NaN;
    public double Top { get; set; } = double.NaN;
    public double Width { get; set; } = 1200;
    public double Height { get; set; } = 800;
    public bool IsMaximized { get; set; }
    public bool SessionPanelOpen { get; set; }
    public double SessionPanelWidth { get; set; } = 260;
    public int ActiveTabIndex { get; set; }
    public List<TabState> Tabs { get; set; } = new();
}

public sealed class TabState
{
    public string Title { get; set; } = "Terminal";
    public string Command { get; set; } = "";
    public string? WorkingDirectory { get; set; }
    public string? StartingCommand { get; set; }
    public string HighlightColor { get; set; } = "";
    public bool Renamed { get; set; }
}
