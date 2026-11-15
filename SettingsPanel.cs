using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace Calculator;

/// <summary>In-window settings overlay: appearance, calculator options and a hotkey for every command.</summary>
public sealed class SettingsPanel : Border
{
    private readonly Settings _s;
    private readonly Action _apply;
    private readonly Button _light, _dark, _deg, _rad;
    private readonly WrapPanel _swatches = new();
    private readonly TextBox _accentHex, _bgHex, _textHex;
    private readonly TextBlock _opacityLabel;
    private readonly Slider _opacity;
    private readonly ComboBox _font;
    private readonly CheckBox _fractions, _onTop;
    private readonly TextBlock _hotkeyNote;
    private readonly Dictionary<string, Button> _keyButtons = new();
    private string? _recording;
    private bool _loading;

    public bool IsRecording => _recording != null;

    public SettingsPanel(Settings settings, Action apply, Action close)
    {
        _s = settings; _apply = apply;
        this[!BackgroundProperty] = this.GetResourceObservable("BgBrush").ToBinding();

        var stack = new StackPanel { Margin = new Thickness(18, 0, 18, 18), MaxWidth = 460 };

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new TextBlock { Text = "Settings", FontSize = 22, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center });
        var done = new Button { Content = "Done", Classes = { "pill", "primary" } };
        done.Click += (_, _) => close();
        Grid.SetColumn(done, 1);
        header.Children.Add(done);
        stack.Children.Add(header);

        // ---- appearance ----
        stack.Children.Add(H("APPEARANCE"));
        stack.Children.Add(Small("Mode"));
        _light = Pill("Light", () => { _s.Mode = "Light"; Changed(); });
        _dark = Pill("Dark", () => { _s.Mode = "Dark"; Changed(); });
        stack.Children.Add(Pills(_light, _dark));

        stack.Children.Add(Small("Accent", 12));
        foreach (var (name, hex) in Themes.Accents)
        {
            var b = new Button
            {
                Width = 30, Height = 30, Padding = new Thickness(0), Margin = new Thickness(0, 0, 6, 6),
                CornerRadius = new CornerRadius(15), Background = Brushes.Transparent, Tag = name,
                Content = new Ellipse { Width = 22, Height = 22, Fill = new SolidColorBrush(Color.Parse(hex)) },
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            ToolTip.SetTip(b, name);
            b.Click += (_, _) => { _s.Accent = name; Changed(); };
            _swatches.Children.Add(b);
        }
        stack.Children.Add(_swatches);
        _accentHex = Field("Custom accent, e.g. #FF9F0A");
        _accentHex.Margin = new Thickness(0, 2, 0, 0);
        _accentHex.TextChanged += (_, _) =>
        {
            if (_loading || !Themes.TryParse(_accentHex.Text, out _)) return;
            _s.Accent = "Custom"; _s.CustomAccent = _accentHex.Text!.Trim(); Changed();
        };
        stack.Children.Add(_accentHex);

        stack.Children.Add(Small("Background colour (empty = default)", 12));
        _bgHex = Field("#000000");
        _bgHex.TextChanged += (_, _) => ColorField(_bgHex, v => _s.CustomBackground = v);
        stack.Children.Add(_bgHex);
        stack.Children.Add(Small("Text colour (empty = default)", 10));
        _textHex = Field("#FFFFFF");
        _textHex.TextChanged += (_, _) => ColorField(_textHex, v => _s.CustomText = v);
        stack.Children.Add(_textHex);

        _opacityLabel = Small("Window opacity", 12);
        stack.Children.Add(_opacityLabel);
        _opacity = new Slider { Minimum = 0.4, Maximum = 1 };
        _opacity.ValueChanged += (_, _) => { if (_loading) return; _s.Opacity = Math.Round(_opacity.Value, 2); Changed(); };
        stack.Children.Add(_opacity);

        stack.Children.Add(Small("Font", 4));
        _font = new ComboBox { ItemsSource = Themes.Fonts, Classes = { "field" }, HorizontalAlignment = HorizontalAlignment.Stretch };
        _font.SelectionChanged += (_, _) => { if (_loading || _font.SelectedItem is not string f) return; _s.Font = f; Changed(); };
        stack.Children.Add(_font);

        // ---- calculator ----
        stack.Children.Add(H("CALCULATOR"));
        stack.Children.Add(Small("Angle unit"));
        _deg = Pill("Degrees", () => { _s.Degrees = true; Changed(); });
        _rad = Pill("Radians", () => { _s.Degrees = false; Changed(); });
        stack.Children.Add(Pills(_deg, _rad));
        _fractions = new CheckBox { Content = "Show results as fractions when you use fractions", Margin = new Thickness(0, 10, 0, 0), FontSize = 13 };
        _fractions.IsCheckedChanged += (_, _) => { if (_loading) return; _s.FractionResults = _fractions.IsChecked == true; Changed(); };
        stack.Children.Add(_fractions);
        _onTop = new CheckBox { Content = "Keep the window on top", FontSize = 13 };
        _onTop.IsCheckedChanged += (_, _) => { if (_loading) return; _s.TopMost = _onTop.IsChecked == true; Changed(); };
        stack.Children.Add(_onTop);
        var tip = Small("Make the window wider to get more keys: the scientific set appears step by step.", 6);
        tip[!TextBlock.ForegroundProperty] = this.GetResourceObservable("SubtleBrush").ToBinding();
        stack.Children.Add(tip);

        // ---- hotkeys ----
        stack.Children.Add(H("HOTKEYS"));
        _hotkeyNote = Small("Click a shortcut and press the keys you want. Esc cancels, ✕ removes it.");
        _hotkeyNote[!TextBlock.ForegroundProperty] = this.GetResourceObservable("SubtleBrush").ToBinding();
        stack.Children.Add(_hotkeyNote);
        foreach (var group in Commands.All.GroupBy(c => c.Group))
        {
            var gh = Small(group.Key, 12);
            gh.FontWeight = FontWeight.SemiBold;
            gh.Margin = new Thickness(0, 12, 0, 4);
            stack.Children.Add(gh);
            foreach (var cmd in group)
            {
                var keyBtn = new Button { Classes = { "pill" }, MinWidth = 110, HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(10, 4) };
                keyBtn.Click += (_, _) => StartRecording(cmd.Id);
                _keyButtons[cmd.Id] = keyBtn;
                var clear = new Button { Content = "✕", Classes = { "pill" }, Margin = new Thickness(4, 0, 0, 0), Padding = new Thickness(9, 4) };
                ToolTip.SetTip(clear, "Remove shortcut");
                clear.Click += (_, _) => SetKey(cmd.Id, "");
                var right = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2) };
                right.Children.Add(keyBtn);
                right.Children.Add(clear);
                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                row.Children.Add(new TextBlock { Text = cmd.Name, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                Grid.SetColumn(right, 1);
                row.Children.Add(right);
                stack.Children.Add(row);
            }
        }
        var resetAll = new Button { Content = "Reset all hotkeys", Classes = { "pill" }, Margin = new Thickness(0, 12, 0, 0) };
        resetAll.Click += (_, _) => { _s.Hotkeys.Clear(); _hotkeyNote.Text = "All hotkeys are back to their defaults."; Changed(); };
        stack.Children.Add(resetAll);

        stack.Children.Add(new TextBlock
        {
            Text = "Settings are saved in " + Settings.DataDir, FontSize = 11, Margin = new Thickness(0, 16, 0, 0),
            TextWrapping = TextWrapping.Wrap, [!TextBlock.ForegroundProperty] = this.GetResourceObservable("SubtleBrush").ToBinding(),
        });

        Child = new ScrollViewer { Content = stack, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Refresh();
    }

    private static TextBlock H(string text) => new() { Text = text, Classes = { "h" } };
    private static TextBlock Small(string text, double top = 0) =>
        new() { Text = text, FontSize = 12, Margin = new Thickness(0, top, 0, 4), TextWrapping = TextWrapping.Wrap };
    private static TextBox Field(string watermark) => new() { Watermark = watermark, Classes = { "field" } };

    private static Button Pill(string text, Action click)
    {
        var b = new Button { Content = text, Classes = { "pill" } };
        b.Click += (_, _) => click();
        return b;
    }

    private static StackPanel Pills(params Button[] buttons)
    {
        var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        p.Children.AddRange(buttons);
        return p;
    }

    private void ColorField(TextBox box, Action<string> set)
    {
        if (_loading) return;
        var t = box.Text?.Trim() ?? "";
        if (t.Length == 0) { set(""); Changed(); }
        else if (Themes.TryParse(t, out _)) { set(t); Changed(); }
    }

    private void Changed()
    {
        _apply();
        Refresh();
    }

    /// <summary>Syncs every control with the current settings.</summary>
    public void Refresh()
    {
        _loading = true;
        _light.Classes.Set("primary", _s.Mode == "Light");
        _dark.Classes.Set("primary", _s.Mode != "Light");
        _deg.Classes.Set("primary", _s.Degrees);
        _rad.Classes.Set("primary", !_s.Degrees);
        var ring = Application.Current!.Resources.TryGetResource("TextBrush", null, out var tb) ? tb as IBrush : Brushes.White;
        foreach (var b in _swatches.Children.OfType<Button>())
        {
            bool on = (string?)b.Tag == _s.Accent;
            b.BorderThickness = new Thickness(on ? 2 : 0);
            b.BorderBrush = ring;
        }
        if (_s.Accent != "Custom" && _accentHex.Text?.Length > 0 && !_accentHex.IsFocused) _accentHex.Text = "";
        else if (_s.Accent == "Custom" && !_accentHex.IsFocused) _accentHex.Text = _s.CustomAccent;
        if (!_bgHex.IsFocused) _bgHex.Text = _s.CustomBackground;
        if (!_textHex.IsFocused) _textHex.Text = _s.CustomText;
        _bgHex.Watermark = _s.Mode == "Light" ? "#F2F2F7" : "#000000";
        _textHex.Watermark = _s.Mode == "Light" ? "#1C1C1E" : "#FFFFFF";
        _opacity.Value = _s.Opacity;
        _opacityLabel.Text = $"Window opacity · {Math.Round(_s.Opacity * 100)}%";
        _font.SelectedItem = Themes.Fonts.Contains(_s.Font) ? _s.Font : Themes.DefaultFont;
        _fractions.IsChecked = _s.FractionResults;
        _onTop.IsChecked = _s.TopMost;
        foreach (var (id, b) in _keyButtons)
            if (id != _recording) b.Content = Commands.Pretty(Commands.KeyFor(_s, id));
        _loading = false;
    }

    // ---------------- hotkey recording ----------------

    private void StartRecording(string id)
    {
        if (_recording != null) _keyButtons[_recording].Content = Commands.Pretty(Commands.KeyFor(_s, _recording));
        _recording = id;
        _keyButtons[id].Content = "Press keys…";
    }

    public void HandleKey(KeyEventArgs e)
    {
        if (_recording == null) return;
        e.Handled = true;
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
            return; // wait for the real key
        var id = _recording;
        _recording = null;
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            _keyButtons[id].Content = Commands.Pretty(Commands.KeyFor(_s, id));
            return;
        }
        SetKey(id, new KeyGesture(e.Key, e.KeyModifiers).ToString());
    }

    private void SetKey(string id, string gesture)
    {
        // A shortcut can only do one thing: take it away from whatever had it.
        if (!string.IsNullOrEmpty(gesture))
        {
            foreach (var other in Commands.All.Where(c => c.Id != id && SameGesture(Commands.KeyFor(_s, c.Id), gesture)))
            {
                _s.Hotkeys[other.Id] = "";
                _hotkeyNote.Text = $"{Commands.Pretty(gesture)} was used by “{other.Name}”; that shortcut was removed.";
            }
        }
        if (Commands.ById[id].DefaultKey == gesture) _s.Hotkeys.Remove(id);
        else _s.Hotkeys[id] = gesture;
        Changed();
    }

    private static bool SameGesture(string a, string b)
    {
        var ga = Commands.Gesture(a); var gb = Commands.Gesture(b);
        return ga != null && gb != null && ga.Key == gb.Key && ga.KeyModifiers == gb.KeyModifiers;
    }
}
