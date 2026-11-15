using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Calculator.MathCore;

namespace Calculator;

/// <summary>In-window list of past calculations. Click one to show it again (and keep calculating with it).</summary>
public sealed class HistoryPanel : Border
{
    private readonly History _history;
    private readonly Action<HistoryEntry> _recall;
    private readonly Action<HistoryEntry> _insert;
    private readonly StackPanel _list = new() { Spacing = 2 };
    private readonly TextBlock _empty;

    public HistoryPanel(History history, Action<HistoryEntry> recall, Action<HistoryEntry> insert, Action close)
    {
        _history = history; _recall = recall; _insert = insert;
        this[!BackgroundProperty] = this.GetResourceObservable("BgBrush").ToBinding();

        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(18, 0, 18, 8) };
        header.Children.Add(new TextBlock { Text = "History", FontSize = 22, FontWeight = FontWeight.Bold, VerticalAlignment = VerticalAlignment.Center });
        var clear = new Button { Content = "Clear", Classes = { "pill" }, Margin = new Thickness(0, 0, 6, 0) };
        clear.Click += (_, _) => _history.Clear();
        Grid.SetColumn(clear, 1);
        header.Children.Add(clear);
        var done = new Button { Content = "Done", Classes = { "pill", "primary" } };
        done.Click += (_, _) => close();
        Grid.SetColumn(done, 2);
        header.Children.Add(done);

        _empty = new TextBlock
        {
            Text = "No calculations yet.", FontSize = 13, Margin = new Thickness(18, 10),
            [!TextBlock.ForegroundProperty] = this.GetResourceObservable("SubtleBrush").ToBinding(),
        };
        var hint = new TextBlock
        {
            Text = "Click to show a calculation again · right-click to insert its result", FontSize = 11,
            Margin = new Thickness(18, 0, 18, 6), TextWrapping = TextWrapping.Wrap,
            [!TextBlock.ForegroundProperty] = this.GetResourceObservable("SubtleBrush").ToBinding(),
        };

        var dock = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        DockPanel.SetDock(hint, Dock.Top);
        DockPanel.SetDock(_empty, Dock.Top);
        dock.Children.Add(header);
        dock.Children.Add(hint);
        dock.Children.Add(_empty);
        dock.Children.Add(new ScrollViewer
        {
            Content = _list, Padding = new Thickness(10, 0, 10, 12),
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        });
        Child = dock;

        _history.Changed += () => { if (IsVisible) Refresh(); };
    }

    public void Refresh()
    {
        _list.Children.Clear();
        _empty.IsVisible = _history.Entries.Count == 0;
        foreach (var e in _history.Entries.Take(100)) _list.Children.Add(Row(e));
    }

    private Control Row(HistoryEntry e)
    {
        var expr = new MathView { FontSize = 17, FontWeight = FontWeight.Normal, Height = 44, MinScale = 0.3 };
        expr[!MathView.ForegroundProperty] = this.GetResourceObservable("SubtleTextBrush").ToBinding();
        expr.Show(e.Expression);
        var resultRow = Formatter.Number(e.Value, false);
        resultRow.Insert(0, new Glyph("= ", "", GlyphKind.Symbol));
        var result = new MathView { FontSize = 26, FontWeight = FontWeight.Light, Height = 38, MinScale = 0.3 };
        result[!MathView.ForegroundProperty] = this.GetResourceObservable("DisplayTextBrush").ToBinding();
        result.Show(resultRow);

        var stack = new StackPanel { Spacing = 0 };
        stack.Children.Add(expr);
        stack.Children.Add(result);

        var card = new Border
        {
            Child = stack, CornerRadius = new CornerRadius(12), Padding = new Thickness(12, 6),
            Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.Hand),
        };
        ToolTip.SetTip(card, e.When.ToString("g"));
        card.PointerEntered += (_, _) => card[!BackgroundProperty] = this.GetResourceObservable("FieldBrush").ToBinding();
        card.PointerExited += (_, _) => card.Background = Brushes.Transparent;
        card.PointerReleased += (_, a) =>
        {
            if (a.InitialPressMouseButton == MouseButton.Right) _insert(e);
            else _recall(e);
        };
        return card;
    }
}
