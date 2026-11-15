using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace Calculator;

public sealed class Settings
{
    // Appearance
    public string Mode { get; set; } = "Dark";
    public string Accent { get; set; } = "Orange";
    public string CustomAccent { get; set; } = "";
    public string CustomBackground { get; set; } = "";
    public string CustomText { get; set; } = "";
    public double Opacity { get; set; } = 1.0;
    public string Font { get; set; } = Themes.DefaultFont;

    /// <summary>Command id → key gesture text. Missing = default, empty string = no hotkey.</summary>
    public Dictionary<string, string> Hotkeys { get; set; } = new();
    public bool Degrees { get; set; } = true;
    public bool TopMost { get; set; }
    public bool FractionResults { get; set; } = true;
    public double Width { get; set; } = 340;
    public double Height { get; set; } = 640;
    public int? X { get; set; }
    public int? Y { get; set; }

    private static string Dir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Calculator");
    private static string FilePath => Path.Combine(Dir, "settings.json");
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), Json) ?? new Settings();
        }
        catch { }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, Json));
        }
        catch { }
    }

    public static string DataDir => Dir;
}

public static class Themes
{
    public static readonly (string Name, string Hex)[] Accents =
    {
        ("Orange",   "#FF9F0A"),
        ("Blue",     "#0A84FF"),
        ("Green",    "#30D158"),
        ("Pink",     "#FF375F"),
        ("Purple",   "#BF5AF2"),
        ("Teal",     "#40C8E0"),
        ("Graphite", "#8E8E93"),
    };

    public const string DefaultFont = "Segoe UI Variable";
    public static readonly string[] Fonts = { "Segoe UI Variable", "Inter", "Bahnschrift", "Cascadia Code", "Georgia" };

    public static FontFamily FontFor(string name) => name == "Inter"
        ? new FontFamily("avares://Avalonia.Fonts.Inter/Assets#Inter")
        : new FontFamily(name);

    public static bool TryParse(string? hex, out Color c)
    {
        c = default;
        return !string.IsNullOrWhiteSpace(hex) && Color.TryParse(hex.Trim(), out c);
    }

    public static Color AccentColor(Settings s)
    {
        var hex = s.Accent == "Custom" ? s.CustomAccent : Accents.FirstOrDefault(a => a.Name == s.Accent).Hex;
        return TryParse(hex, out var c) ? c : Color.Parse(Accents[0].Hex);
    }

    private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    private static double Luma(Color c) => (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255;

    public static void Apply(Settings s)
    {
        var app = Application.Current!;
        bool dark = s.Mode != "Light";
        app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;

        var accent = AccentColor(s);
        var bg = TryParse(s.CustomBackground, out var cb) ? cb : Color.Parse(dark ? "#000000" : "#F2F2F7");
        var text = TryParse(s.CustomText, out var ct) ? ct : Color.Parse(dark ? "#FFFFFF" : "#1C1C1E");
        byte alpha = (byte)Math.Clamp(s.Opacity * 255, 90, 255);
        var onAccent = Luma(accent) > 0.75 ? Colors.Black : Colors.White;

        void Set(string key, Color c) => app.Resources[key] = new SolidColorBrush(c);
        void KeyFamily(string prefix, Color fill, Color fg)
        {
            var shift = Luma(fill) > 0.6 ? Colors.Black : Colors.White;
            Set(prefix + "Brush", fill);
            Set(prefix + "HoverBrush", Mix(fill, shift, 0.12));
            Set(prefix + "PressBrush", Mix(fill, shift, 0.28));
            Set(prefix + "TextBrush", fg);
        }

        // Window & display
        Set("BgBrush", Color.FromArgb(alpha, bg.R, bg.G, bg.B));
        Set("DisplayTextBrush", text);
        Set("TextBrush", text);
        Set("SubtleTextBrush", Color.FromArgb(150, text.R, text.G, text.B));
        Set("SubtleBrush", Color.FromArgb(150, text.R, text.G, text.B));
        Set("CursorBrush", accent);
        Set("ErrorBrush", Color.Parse("#FF6961"));
        Set("FieldBrush", Mix(bg, text, 0.09));
        Set("FrameBrush", Color.FromArgb(dark ? (byte)45 : (byte)30, text.R, text.G, text.B));
        Set("HoverBrush", Color.FromArgb(40, text.R, text.G, text.B));
        Set("AccentBrush", accent);
        Set("OnAccentBrush", onAccent);

        // Keys are derived from background + text, so any custom colours stay coherent.
        if (dark)
        {
            KeyFamily("Num", Mix(bg, text, 0.20), text);
            KeyFamily("Fn", Mix(bg, text, 0.12), text);
            KeyFamily("Top", Mix(bg, text, 0.65), bg);
        }
        else
        {
            KeyFamily("Num", Mix(bg, Colors.White, 0.9), text);
            KeyFamily("Fn", Mix(bg, text, 0.07), text);
            KeyFamily("Top", Mix(bg, text, 0.17), text);
        }
        KeyFamily("Op", accent, onAccent);

        app.Resources["SystemAccentColor"] = accent;
        app.Resources["SystemAccentColorLight1"] = accent;
        app.Resources["SystemAccentColorLight2"] = accent;
        app.Resources["SystemAccentColorDark1"] = accent;
        app.Resources["SystemAccentColorDark2"] = accent;
        app.Resources["AppFont"] = FontFor(s.Font);
    }
}
