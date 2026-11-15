using Calculator.MathCore;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;

namespace Calculator;

public sealed class MainWindow : Window
{
    private readonly Settings _settings;
    private readonly CalcEngine _calc = new();
    private readonly MathView _main = new() { FontWeight = FontWeight.Light };
    private readonly MathView _top = new() { FontWeight = FontWeight.Normal, FontSize = 20, ShowZero = false };
    private readonly TextBlock _error = new() { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, FontSize = 16, IsVisible = false };
    private readonly Grid _keys = new();
    private readonly Grid _display = new() { RowDefinitions = new RowDefinitions("*,Auto,Auto") };
    private readonly Button _angleChip, _pinChip, _memChip, _gear;
    private readonly SettingsPanel _settingsPanel;
    private readonly HistoryPanel _historyPanel;
    private readonly Button _historyChip;
    private readonly Dictionary<string, Button> _buttons = new();
    private List<(KeyGesture Gesture, string Id)> _hotkeys = new();
    private int _tier = -1;
    private bool _suppressText;

    public MainWindow(Settings settings)
    {
        _settings = settings;
        _calc.Eval.Degrees = settings.Degrees;
        _calc.FractionMode = settings.FractionResults;

        Title = "Calculator";
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Calculator/Calculator.ico")));
        Width = settings.Width; Height = settings.Height;
        MinWidth = 300; MinHeight = 470;
        if (settings.X is int x && settings.Y is int y && OnSomeScreen(x, y)) { Position = new PixelPoint(x, y); WindowStartupLocation = WindowStartupLocation.Manual; }
        else WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Topmost = settings.TopMost;
        // Fully custom frame: rounded corners and traffic-light buttons.
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        this[!FontFamilyProperty] = this.GetResourceObservable("AppFont").ToBinding();
        this[!ForegroundProperty] = this.GetResourceObservable("TextBrush").ToBinding();

        _main[!MathView.ForegroundProperty] = this.GetResourceObservable("DisplayTextBrush").ToBinding();
        _main[!MathView.CursorBrushProperty] = this.GetResourceObservable("CursorBrush").ToBinding();
        _top[!MathView.ForegroundProperty] = this.GetResourceObservable("SubtleTextBrush").ToBinding();
        _error[!TextBlock.ForegroundProperty] = this.GetResourceObservable("ErrorBrush").ToBinding();
        _main.Cursor = new Cursor(StandardCursorType.Ibeam);

        // ---- title bar chips ----
        _gear = Chip("", "Settings");
        _gear.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        _gear.Click += (_, _) => ToggleSettings();
        _historyChip = Chip("\uE81C", "History");
        _historyChip.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        _historyChip.Click += (_, _) => ToggleHistory();
        _angleChip = Chip("DEG", "Degrees / radians");
        _angleChip.Click += (_, _) => Run("angle");
        _pinChip = Chip("", "Keep on top");
        _pinChip.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        _pinChip.Click += (_, _) => Run("topmost");
        _memChip = Chip("M", "Memory recall");
        _memChip.Click += (_, _) => Run("mr");
        var left = Chip("", "Cursor left");
        left.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        left.Click += (_, _) => Run("left");
        var right = Chip("", "Cursor right");
        right.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
        right.Click += (_, _) => Run("right");

        var chips = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(14, 4, 0, 0),
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Left,
        };
        var lights = new TrafficLights(this) { Margin = new Thickness(0, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        chips.Children.AddRange(new Control[] { lights, _gear, _historyChip, _angleChip, _pinChip, _memChip, left, right });

        // ---- display ----
        var topRow = new Grid();
        topRow.Children.Add(_top);
        topRow.Children.Add(_error);
        Grid.SetRow(topRow, 1);
        Grid.SetRow(_main, 2);
        _top.Height = 34;
        _main.Margin = new Thickness(0, 2, 0, 4);
        _display.Children.Add(topRow);
        _display.Children.Add(_main);
        _display.Margin = new Thickness(18, 0, 18, 6);
        _display.Background = Brushes.Transparent;
        _display.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;
            if (_calc.ShowingResult) { Run("end"); return; }
            // Click inside the expression to put the cursor there (numerator, denominator, root index, …).
            if (_main.HitTest(e.GetPosition(_main)) is { } hit) _calc.PlaceCursor(hit.Row, hit.Index);
        };
        _display.ContextMenu = BuildMenu();

        _keys.Margin = new Thickness(10, 0, 10, 12);

        _settingsPanel = new SettingsPanel(settings, ApplySettings, () => ToggleSettings(false)) { IsVisible = false };
        _historyPanel = new HistoryPanel(_calc.History,
            e => { ToggleHistory(false); _calc.Recall(e); },
            e => { ToggleHistory(false); _calc.InsertValue(e.Value); },
            () => ToggleHistory(false)) { IsVisible = false };

        var root = new Grid { RowDefinitions = new RowDefinitions("38,2*,5*") };
        var titleDrag = new Border { Background = Brushes.Transparent };
        titleDrag.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            if (e.ClickCount == 2) ToggleMaximize(); else BeginMoveDrag(e);
        };
        root.Children.Add(titleDrag);
        root.Children.Add(chips);
        Grid.SetRow(_display, 1);
        Grid.SetRow(_keys, 2);
        Grid.SetRow(_settingsPanel, 1);
        Grid.SetRowSpan(_settingsPanel, 2);
        root.Children.Add(_display);
        root.Children.Add(_keys);
        Grid.SetRow(_historyPanel, 1);
        Grid.SetRowSpan(_historyPanel, 2);
        root.Children.Add(_historyPanel);
        root.Children.Add(_settingsPanel);
        _frame = new Border
        {
            CornerRadius = new CornerRadius(18),
            ClipToBounds = true,
            BorderThickness = new Thickness(1),
            Child = root,
        };
        _frame[!Border.BackgroundProperty] = this.GetResourceObservable("BgBrush").ToBinding();
        _frame[!Border.BorderBrushProperty] = this.GetResourceObservable("FrameBrush").ToBinding();
        Content = _frame;
        AddHandler(PointerPressedEvent, OnResizePress, RoutingStrategies.Tunnel);
        PointerMoved += OnResizeHover;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty)
                _frame.CornerRadius = new CornerRadius(WindowState == WindowState.Maximized ? 0 : 18);
        };

        LoadHotkeys();
        _calc.Changed += Refresh;

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        AddHandler(TextInputEvent, OnTextInput, RoutingStrategies.Tunnel);
        SizeChanged += (_, _) => UpdateLayoutTier();
        _display.SizeChanged += (_, _) => UpdateLayoutTier();
        Closing += (_, _) => SaveWindow();
        Opened += (_, _) => { UpdateLayoutTier(); Refresh(); };
    }

    private Border _frame = null!;

    public void ToggleMaximize() =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    // ---------------- resizing a borderless window ----------------

    private WindowEdge? EdgeAt(Point p)
    {
        if (WindowState != WindowState.Normal) return null;
        const double g = 7, c = 16; // edge grip, and a bigger grip at the rounded corners
        double w = Bounds.Width, h = Bounds.Height;
        bool l = p.X < g, r = p.X > w - g, t = p.Y < g, b = p.Y > h - g;
        bool lc = p.X < c, rc = p.X > w - c, tc = p.Y < c, bc = p.Y > h - c;
        if ((t && lc) || (l && tc)) return WindowEdge.NorthWest;
        if ((t && rc) || (r && tc)) return WindowEdge.NorthEast;
        if ((b && lc) || (l && bc)) return WindowEdge.SouthWest;
        if ((b && rc) || (r && bc)) return WindowEdge.SouthEast;
        if (l) return WindowEdge.West;
        if (r) return WindowEdge.East;
        if (t) return WindowEdge.North;
        if (b) return WindowEdge.South;
        return null;
    }

    private void OnResizeHover(object? sender, PointerEventArgs e)
    {
        Cursor = EdgeAt(e.GetPosition(this)) switch
        {
            WindowEdge.West or WindowEdge.East => new Cursor(StandardCursorType.SizeWestEast),
            WindowEdge.North or WindowEdge.South => new Cursor(StandardCursorType.SizeNorthSouth),
            WindowEdge.NorthWest => new Cursor(StandardCursorType.TopLeftCorner),
            WindowEdge.SouthEast => new Cursor(StandardCursorType.BottomRightCorner),
            WindowEdge.NorthEast => new Cursor(StandardCursorType.TopRightCorner),
            WindowEdge.SouthWest => new Cursor(StandardCursorType.BottomLeftCorner),
            _ => Cursor.Default,
        };
    }

    private void OnResizePress(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (EdgeAt(e.GetPosition(this)) is { } edge) { e.Handled = true; BeginResizeDrag(edge, e); }
    }

    private ContextMenu BuildMenu() => new()
    {
        Items = { MenuItemFor("Copy result", "copy"), MenuItemFor("Paste", "paste"), MenuItemFor("Show as fraction / decimal", "sd") },
    };

    private MenuItem MenuItemFor(string header, string id)
    {
        var m = new MenuItem { Header = header, InputGesture = Commands.Gesture(Commands.KeyFor(_settings, id)) };
        m.Click += (_, _) => Run(id);
        return m;
    }

    private static Button Chip(string text, string tip)
    {
        var b = new Button { Content = text, Classes = { "chrome" } };
        ToolTip.SetTip(b, tip);
        return b;
    }

    private bool OnSomeScreen(int x, int y) =>
        Screens.All.Any(s => s.WorkingArea.Contains(new PixelPoint(x + 40, y + 20)));

    // ---------------- layout tiers ----------------

    private void UpdateLayoutTier()
    {
        double w = ClientSize.Width;
        int tier = w < 450 ? 0 : w < 730 ? 1 : 2;
        if (tier != _tier) { _tier = tier; BuildKeys(); }

        double dispH = _display.Bounds.Height;
        if (dispH > 0)
        {
            _main.FontSize = Math.Clamp(dispH * 0.40, 30, 84);
            _top.FontSize = Math.Clamp(_main.FontSize * 0.42, 15, 26);
            _top.Height = _top.FontSize * 1.9;
            _error.FontSize = _top.FontSize * 0.85;
            _main.MaxHeight = Math.Max(30, dispH - _top.Height - 8);
        }
    }

    private void BuildKeys()
    {
        _keys.Children.Clear();
        _buttons.Clear();
        var rows = Commands.Compact.Select((r, i) =>
        {
            IEnumerable<string> ids = r;
            if (_tier >= 1) ids = Commands.Medium[i].Concat(ids);
            if (_tier >= 2) ids = Commands.Wide[i].Concat(ids);
            return ids.ToArray();
        }).ToArray();

        int cols = rows[0].Length;
        _keys.ColumnDefinitions = new ColumnDefinitions(string.Join(",", Enumerable.Repeat("*", cols)));
        _keys.RowDefinitions = new RowDefinitions(string.Join(",", Enumerable.Repeat("*", rows.Length)));

        for (int r = 0; r < rows.Length; r++)
        for (int c = 0; c < rows[r].Length; c++)
        {
            var cmd = Commands.ById[rows[r][c]];
            var b = MakeKey(cmd);
            Grid.SetRow(b, r);
            Grid.SetColumn(b, c);
            _keys.Children.Add(b);
            _buttons[cmd.Id] = b;
        }
        Refresh();
    }

    private Button MakeKey(Command cmd)
    {
        var b = new Button
        {
            Classes =
            {
                "key",
                cmd.Style switch { KeyStyle.Number => "num", KeyStyle.Utility => "top", KeyStyle.Operator => "op", _ => "fn" },
            },
            Margin = new Thickness(5),
            Content = KeyLabel(cmd),
        };

        var key = Commands.KeyFor(_settings, cmd.Id);
        ToolTip.SetTip(b, string.IsNullOrEmpty(key) ? cmd.Name : $"{cmd.Name}   ({Commands.Pretty(key)})");
        ToolTip.SetShowDelay(b, 700);
        b.Click += (_, _) => Run(cmd.Id);
        b.SizeChanged += (_, e) => SizeKey(b, cmd, e.NewSize);
        return b;
    }

    /// <summary>Every key label is drawn by the same math renderer in the app font, so all symbols match.</summary>
    private static Control KeyLabel(Command cmd)
    {
        if (cmd.Id == "back") return new BackspaceIcon();

        var row = new MathRow();
        void T(MathRow r, string s, GlyphKind k = GlyphKind.Symbol) => r.Insert(r.Items.Count, new Glyph(s, "", k));
        switch (cmd.Id)
        {
            case "sqrt": { var n = new Root(true); T(n.Radicand, "x"); row.Insert(0, n); break; }
            case "cbrt": { var n = new Root(false); T(n.Index, "3"); T(n.Radicand, "x"); row.Insert(0, n); break; }
            case "nroot": { var n = new Root(false); T(n.Index, "n"); T(n.Radicand, "x"); row.Insert(0, n); break; }
            case "frac": { var n = new Fraction(); T(n.Num, "a"); T(n.Den, "b"); row.Insert(0, n); break; }
            case "inv": { var n = new Fraction(); T(n.Num, "1"); T(n.Den, "x"); row.Insert(0, n); break; }
            case "abs": { var n = new Abs(); T(n.Inner, "x"); row.Insert(0, n); break; }
            case "pow": { T(row, "x"); var p = new Power(); T(p.Exp, "y"); row.Insert(1, p); break; }
            case "square": { T(row, "x"); var p = new Power(); T(p.Exp, "2"); row.Insert(1, p); break; }
            case "cube": { T(row, "x"); var p = new Power(); T(p.Exp, "3"); row.Insert(1, p); break; }
            case "exp": { T(row, "e"); var p = new Power(); T(p.Exp, "x"); row.Insert(1, p); break; }
            case "pow10": { T(row, "10"); var p = new Power(); T(p.Exp, "x"); row.Insert(1, p); break; }
            case "pow2": { T(row, "2"); var p = new Power(); T(p.Exp, "x"); row.Insert(1, p); break; }
            default: T(row, cmd.Label); break;
        }
        var view = new MathView { Center = true, FontWeight = FontWeight.Normal, MinScale = 0.2, IsHitTestVisible = false };
        view.Show(row);
        return view;
    }

    private static void SizeKey(Button b, Command cmd, Size size)
    {
        double w = size.Width, h = size.Height;
        if (w <= 0 || h <= 0) return;
        b.CornerRadius = new CornerRadius(Math.Min(w, h) / 2);
        double fs = cmd.Style switch
        {
            KeyStyle.Number => Math.Min(h * 0.44, w * 0.42),
            KeyStyle.Operator => Math.Min(h * 0.50, w * 0.48),
            _ => Math.Min(h * 0.36, w * 0.36),
        };
        fs = Math.Max(10, fs);
        if (b.Content is MathView mv)
        {
            mv.FontSize = fs;
            // Room for the label inside the pill; long labels shrink to fit.
            mv.Width = w * 0.8;
            mv.Height = h * 0.8;
        }
        else if (b.Content is BackspaceIcon bi) { bi.Width = fs * 1.3; bi.Height = fs; }
    }

    // ---------------- running commands ----------------

    private async void Run(string id)
    {
        switch (id)
        {
            case "settings": ToggleSettings(); return;
            case "history": ToggleHistory(); return;
            case "topmost":
                Topmost = !Topmost;
                _settings.TopMost = Topmost;
                _settings.Save();
                Refresh();
                return;
            case "copy":
                if (_calc.CopyText() is { } text && Clipboard is { } cb) await cb.SetTextAsync(text);
                Flash("copy");
                return;
            case "paste":
                if (Clipboard is { } cb2 && await cb2.GetTextAsync() is { } pasted) _calc.PasteText(pasted);
                return;
        }
        _calc.Run(id);
        if (id == "angle") { _settings.Degrees = _calc.Eval.Degrees; _settings.Save(); }
        Flash(id);
    }

    // Briefly press the key a hotkey triggered.
    private void Flash(string id)
    {
        if (!_buttons.TryGetValue(id, out var b)) return;
        b.RenderTransform = Avalonia.Media.Transformation.TransformOperations.Parse("scale(0.94)");
        Avalonia.Threading.DispatcherTimer.RunOnce(() => b.RenderTransform = null, TimeSpan.FromMilliseconds(90));
    }

    private void Refresh()
    {
        if (_calc.ShowingResult && _calc.Result is { } r)
        {
            _top.Show(_calc.LastExpression ?? new MathRow());
            _main.Show(Formatter.Number(r, _calc.ResultAsFraction));
        }
        else
        {
            _main.Show(_calc.Editor.Root, _calc.Editor.Row, _calc.Editor.Index);
            var preview = _calc.Preview();
            if (preview is { } p && !_calc.ExpressionIsPlainNumber)
            {
                var row = Formatter.Number(p, _calc.ResultAsFraction);
                row.Insert(0, new Glyph("= ", "", GlyphKind.Symbol));
                _top.Show(row);
            }
            else _top.Show(new MathRow());
        }
        bool err = _calc.Error != null;
        _error.Text = _calc.Error;
        _error.IsVisible = err;
        _top.IsVisible = !err && (_calc.ShowingResult || (_calc.Preview() is not null && !_calc.ExpressionIsPlainNumber));

        _angleChip.Content = _calc.Eval.Degrees ? "DEG" : "RAD";
        SetLabel("angle", _calc.Eval.Degrees ? "Rad" : "Deg");
        SetLabel("clear", _calc.Editor.IsEmpty && !_calc.ShowingResult ? "AC" : "C");
        _memChip.IsVisible = _calc.HasMemory;
        _pinChip.Classes.Set("on", Topmost);
    }

    private void SetLabel(string id, string text)
    {
        if (!_buttons.TryGetValue(id, out var b) || b.Content is not MathView mv) return;
        var row = new MathRow();
        row.Insert(0, new Glyph(text, ""));
        mv.Show(row);
    }

    // ---------------- keyboard ----------------

    private void LoadHotkeys()
    {
        _hotkeys = Commands.All
            .Select(c => (Gesture: Commands.Gesture(Commands.KeyFor(_settings, c.Id)), c.Id))
            .Where(t => t.Gesture != null)
            .Select(t => (t.Gesture!, t.Id))
            .ToList();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        _suppressText = false;
        if (_historyPanel.IsVisible && e.Key == Key.Escape) { ToggleHistory(false); e.Handled = true; return; }
        if (_settingsPanel.IsVisible)
        {
            if (_settingsPanel.IsRecording) { _settingsPanel.HandleKey(e); return; }
            if (e.Key == Key.Escape) { ToggleSettings(false); e.Handled = true; }
            return;
        }
        if (e.Source is TextBox) return;
        foreach (var (g, id) in _hotkeys)
        {
            if (!g.Matches(e)) continue;
            e.Handled = true;
            _suppressText = true;
            Run(id);
            return;
        }
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (_settingsPanel.IsVisible) return;
        if (_historyPanel.IsVisible && !_suppressText) ToggleHistory(false);
        if (_suppressText) { _suppressText = false; e.Handled = true; return; }
        if (string.IsNullOrEmpty(e.Text)) return;
        foreach (var ch in e.Text)
        {
            if (!_calc.Type(ch)) continue;
            e.Handled = true;
            var id = ch switch
            {
                >= '0' and <= '9' => "d" + ch, '.' or ',' => "dot", '+' => "add", '-' => "sub",
                '*' or 'x' => "mul", '/' => "div", '=' => "eq", '%' => "percent", '(' => "lparen", ')' => "rparen", _ => "",
            };
            Flash(id);
        }
    }

    // ---------------- settings ----------------

    private void ToggleHistory(bool? show = null)
    {
        bool s = show ?? !_historyPanel.IsVisible;
        if (s) ToggleSettings(false);
        _historyPanel.IsVisible = s;
        _historyChip.Classes.Set("on", s);
        if (s) _historyPanel.Refresh();
    }

    private void ToggleSettings(bool? show = null)
    {
        bool s = show ?? !_settingsPanel.IsVisible;
        if (s && _historyPanel.IsVisible) ToggleHistory(false);
        _settingsPanel.IsVisible = s;
        _gear.Classes.Set("on", s);
        if (s) _settingsPanel.Refresh();
    }

    private void ApplySettings()
    {
        Themes.Apply(_settings);
        _calc.Eval.Degrees = _settings.Degrees;
        _calc.FractionMode = _settings.FractionResults;
        Topmost = _settings.TopMost;
        LoadHotkeys();
        _tier = -1;
        UpdateLayoutTier(); // rebuild keys so tooltips show new hotkeys
        _display.ContextMenu = BuildMenu();
        _settings.Save();
        Refresh();
    }

    private void SaveWindow()
    {
        if (WindowState == WindowState.Normal)
        {
            _settings.Width = Width; _settings.Height = Height;
            _settings.X = Position.X; _settings.Y = Position.Y;
        }
        _settings.Save();
    }
}

/// <summary>The ⌫ key, drawn as a shape so it doesn't depend on any font.</summary>
public sealed class BackspaceIcon : Control
{
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        Avalonia.Controls.Documents.TextElement.ForegroundProperty.AddOwner<BackspaceIcon>();
    static BackspaceIcon() => AffectsRender<BackspaceIcon>(ForegroundProperty);
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }

    public BackspaceIcon() { IsHitTestVisible = false; }

    public override void Render(DrawingContext dc)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;
        var pen = new Pen(Foreground ?? Brushes.White, Math.Max(1.3, h * 0.08), lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
        double t = h * 0.15, b = h * 0.85, l = w * 0.06, r = w * 0.94, tip = w * 0.32;
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(l, h / 2), false);
            c.LineTo(new Point(tip, t));
            c.LineTo(new Point(r, t));
            c.LineTo(new Point(r, b));
            c.LineTo(new Point(tip, b));
            c.EndFigure(true);
        }
        dc.DrawGeometry(null, pen, g);
        double cx = (tip + r) / 2 + w * 0.02, cy = h / 2, a = h * 0.16;
        dc.DrawLine(pen, new Point(cx - a, cy - a), new Point(cx + a, cy + a));
        dc.DrawLine(pen, new Point(cx - a, cy + a), new Point(cx + a, cy - a));
    }
}

/// <summary>macOS-style close / minimise / full-screen buttons. Symbols appear while the mouse is over them.</summary>
public sealed class TrafficLights : StackPanel
{
    public TrafficLights(MainWindow w)
    {
        Orientation = Orientation.Horizontal;
        Spacing = 8;
        Background = Brushes.Transparent;
        var close = new Light("#FF5F57", "#4D0000", Light.Glyph.Close, () => w.Close(), "Close");
        var min = new Light("#FEBC2E", "#5A3A00", Light.Glyph.Minimize, () => w.WindowState = WindowState.Minimized, "Minimize");
        var max = new Light("#28C840", "#003A00", Light.Glyph.Zoom, w.ToggleMaximize, "Full screen");
        Children.AddRange(new Control[] { close, min, max });
        PointerEntered += (_, _) => { foreach (var l in Children.OfType<Light>()) l.ShowGlyph(true); };
        PointerExited += (_, _) => { foreach (var l in Children.OfType<Light>()) l.ShowGlyph(false); };
    }

    private sealed class Light : Control
    {
        public enum Glyph { Close, Minimize, Zoom }
        private readonly IBrush _fill, _ink;
        private readonly Glyph _glyph;
        private readonly Action _click;
        private bool _show, _down;

        public Light(string fill, string ink, Glyph glyph, Action click, string tip)
        {
            _fill = new SolidColorBrush(Color.Parse(fill));
            _ink = new SolidColorBrush(Color.Parse(ink));
            _glyph = glyph; _click = click;
            Width = 13; Height = 13;
            ToolTip.SetTip(this, tip);
            ToolTip.SetShowDelay(this, 900);
        }

        public void ShowGlyph(bool on) { _show = on; InvalidateVisual(); }

        protected override void OnPointerPressed(PointerPressedEventArgs e) { _down = true; e.Handled = true; InvalidateVisual(); }
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            bool inside = new Rect(Bounds.Size).Contains(e.GetPosition(this));
            bool wasDown = _down;
            _down = false; e.Handled = true; InvalidateVisual();
            if (wasDown && inside) _click();
        }

        public override void Render(DrawingContext dc)
        {
            double s = Bounds.Width, c = s / 2;
            dc.DrawEllipse(_fill, new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), 0.6), new Point(c, c), c - 0.3, c - 0.3);
            if (_down) dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(50, 0, 0, 0)), null, new Point(c, c), c, c);
            if (!_show) return;
            var pen = new Pen(_ink, 1.3, lineCap: PenLineCap.Round);
            double a = s * 0.23;
            switch (_glyph)
            {
                case Glyph.Close:
                    dc.DrawLine(pen, new Point(c - a, c - a), new Point(c + a, c + a));
                    dc.DrawLine(pen, new Point(c - a, c + a), new Point(c + a, c - a));
                    break;
                case Glyph.Minimize:
                    dc.DrawLine(pen, new Point(c - a * 1.25, c), new Point(c + a * 1.25, c));
                    break;
                case Glyph.Zoom:
                {
                    double k = a * 1.15;
                    var g = new StreamGeometry();
                    using (var ctx = g.Open())
                    {
                        ctx.BeginFigure(new Point(c - k, c - k), true);
                        ctx.LineTo(new Point(c + k * 0.35, c - k));
                        ctx.LineTo(new Point(c - k, c + k * 0.35));
                        ctx.EndFigure(true);
                        ctx.BeginFigure(new Point(c + k, c + k), true);
                        ctx.LineTo(new Point(c - k * 0.35, c + k));
                        ctx.LineTo(new Point(c + k, c - k * 0.35));
                        ctx.EndFigure(true);
                    }
                    dc.DrawGeometry(_ink, null, g);
                    break;
                }
            }
        }
    }
}
