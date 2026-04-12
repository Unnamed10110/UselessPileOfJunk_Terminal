using System.IO;
using System.Text.Json;

namespace UselessTerminal.Models;

public sealed class KeyBindingConfig
{
    private static readonly string StorePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "UselessTerminal", "keybindings.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public Dictionary<string, string> Bindings { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static KeyBindingConfig Instance { get; } = Load();

    private static KeyBindingConfig Load()
    {
        try
        {
            if (File.Exists(StorePath))
            {
                string json = File.ReadAllText(StorePath);
                var cfg = JsonSerializer.Deserialize<KeyBindingConfig>(json, JsonOpts);
                if (cfg is not null)
                {
                    EnsureDefaults(cfg);
                    return cfg;
                }
            }
        }
        catch { }

        var defaults = new KeyBindingConfig();
        EnsureDefaults(defaults);
        return defaults;
    }

    public void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(StorePath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(StorePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch { }
    }

    private static void EnsureDefaults(KeyBindingConfig cfg)
    {
        void D(string action, string combo)
        {
            cfg.Bindings.TryAdd(action, combo);
        }

        D("newTab", "Ctrl+T");
        D("closePane", "Ctrl+W");
        D("togglePanel", "Ctrl+B");
        D("settings", "Ctrl+OemComma");
        D("nextTab", "Ctrl+Tab");
        D("prevTab", "Ctrl+Shift+Tab");
        D("newSession", "Ctrl+Shift+N");
        D("duplicateTab", "Ctrl+Shift+D");
        D("commandPalette", "Ctrl+Shift+P");
        D("quickConnect", "Ctrl+Shift+O");
        D("movePaneFocus", "Ctrl+Shift+Arrow");
        D("selectTab1", "Ctrl+D1");
        D("selectTab2", "Ctrl+D2");
        D("selectTab3", "Ctrl+D3");
        D("selectTab4", "Ctrl+D4");
        D("selectTab5", "Ctrl+D5");
        D("selectTab6", "Ctrl+D6");
        D("selectTab7", "Ctrl+D7");
        D("selectTab8", "Ctrl+D8");
        D("selectTab9", "Ctrl+D9");
    }

    /// <summary>Check if a key combination matches the configured binding for an action.</summary>
    public bool Matches(string action, bool ctrl, bool shift, bool alt, System.Windows.Input.Key key)
    {
        if (!Bindings.TryGetValue(action, out string? combo) || string.IsNullOrEmpty(combo))
            return false;

        bool wantCtrl = false, wantShift = false, wantAlt = false;
        string keyPart = "";

        foreach (string part in combo.Split('+'))
        {
            string p = part.Trim();
            if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) wantCtrl = true;
            else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) wantShift = true;
            else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) wantAlt = true;
            else keyPart = p;
        }

        if (ctrl != wantCtrl || shift != wantShift || alt != wantAlt) return false;

        if (keyPart.Equals("Arrow", StringComparison.OrdinalIgnoreCase))
        {
            return key == System.Windows.Input.Key.Left || key == System.Windows.Input.Key.Right
                || key == System.Windows.Input.Key.Up || key == System.Windows.Input.Key.Down;
        }

        if (Enum.TryParse<System.Windows.Input.Key>(keyPart, ignoreCase: true, out var expected))
            return key == expected;

        return false;
    }
}
