using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Calculator.MathCore;

/// <summary>
/// Draws a math row as real 2-D math: stacked fractions, raised exponents, radicals with an index.
/// Shrinks to fit the available width and draws a blinking cursor when editing.
/// </summary>
public sealed class MathView : Control
{
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<MathView, double>(nameof(FontSize), 40);
    // Foreground and font are inherited, so a MathView inside a key button picks up the key's colours.
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        Avalonia.Controls.Documents.TextElement.ForegroundProperty.AddOwner<MathView>();
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        Avalonia.Controls.Documents.TextElement.FontFamilyProperty.AddOwner<MathView>();
    public static readonly StyledProperty<IBrush?> CursorBrushProperty =
        AvaloniaProperty.Register<MathView, IBrush?>(nameof(CursorBrush), Brushes.Orange);
    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        AvaloniaProperty.Register<MathView, FontWeight>(nameof(FontWeight), FontWeight.Light);

    static MathView()
    {
        AffectsMeasure<MathView>(FontSizeProperty, FontWeightProperty, FontFamilyProperty);
        AffectsRender<MathView>(ForegroundProperty, CursorBrushProperty);
    }

    public double FontSize { get => GetValue(FontSizeProperty); set => SetValue(FontSizeProperty, value); }
    public IBrush? Foreground { get => GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public FontFamily FontFamily { get => GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public IBrush? CursorBrush { get => GetValue(CursorBrushProperty); set => SetValue(CursorBrushProperty, value); }
    public FontWeight FontWeight { get => GetValue(FontWeightProperty); set => SetValue(FontWeightProperty, value); }

    /// <summary>Centre the math (for key labels) instead of right/bottom aligning it (for the display).</summary>
    public bool Center { get; set; }

    /// <summary>Show "0" for an empty expression (the main display) or nothing (the preview line).</summary>
    public bool ShowZero { get; set; } = true;

    private MathRow? _row;
    private MathRow? _cursorRow;
    private int _cursorIndex;
    private bool _blinkOn = true;
    private DispatcherTimer? _blink;
    private Box? _layout;

    /// <summary>Minimum scale before the view stops shrinking long expressions.</summary>
    public double MinScale { get; set; } = 0.35;

    public MathView()
    {
        ClipToBounds = true;
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FontSizeProperty || change.Property == FontWeightProperty || change.Property == FontFamilyProperty)
            _layout = null;
    }

    public void Show(MathRow row, MathRow? cursorRow = null, int cursorIndex = 0)
    {
        _row = row; _cursorRow = cursorRow; _cursorIndex = cursorIndex;
        _blinkOn = true;
        if (cursorRow != null)
        {
            if (_blink == null)
            {
                _blink = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
                _blink.Tick += (_, _) => { _blinkOn = !_blinkOn; if (_cursorRow != null) InvalidateVisual(); };
            }
            _blink.Stop(); _blink.Start();
        }
        else _blink?.Stop();
        _layout = null;
        InvalidateMeasure();
        InvalidateVisual();
    }

    private Box Layout()
    {
        if (_layout != null) return _layout;
        var ctx = new LayoutCtx(new Typeface(FontFamily, FontStyle.Normal, FontWeight), _cursorRow);
        _layout = ctx.Row(_row ?? new MathRow(), FontSize, isTop: !Center && ShowZero);
        return _layout;
    }

    private double FitScale(Size space, double w, double h)
    {
        double scale = 1;
        if (!double.IsInfinity(space.Width) && w > space.Width) scale = space.Width / w;
        if (!double.IsInfinity(space.Height) && h * scale > space.Height) scale = space.Height / h;
        return Math.Max(MinScale, scale);
    }

    protected override Size MeasureOverride(Size available)
    {
        var b = Layout();
        double w = b.W, h = b.A + b.D;
        double scale = FitScale(available, w, h);
        return new Size(Math.Min(w * scale, available.Width), Math.Min(h * scale, available.Height));
    }

    public override void Render(DrawingContext dc)
    {
        var b = Layout();
        double w = b.W, h = b.A + b.D;
        if (w <= 0 || h <= 0) return;
        double scale = FitScale(Bounds.Size, w, h);
        // Right-aligned and sitting on the bottom edge, like a calculator display.
        double x = Center ? (Bounds.Width - w * scale) / 2 : Bounds.Width - w * scale;
        double y = Center ? (Bounds.Height - h * scale) / 2 : Math.Max(0, Bounds.Height - h * scale);
        using (dc.PushTransform(Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(x, y)))
        {
            var draw = new DrawCtx(dc, Foreground ?? Brushes.White, CursorBrush ?? Brushes.Orange,
                _cursorRow, _cursorIndex, _blinkOn && _cursorRow != null) { Hits = new() };
            b.Draw(draw, 0, b.A);
            _hits = draw.Hits;
            _scale = scale; _ox = x; _oy = y;
        }
    }

    private List<Hit> _hits = new();
    private double _scale = 1, _ox, _oy;

    /// <summary>
    /// The cursor position (row + index) closest to a point in this control, e.g. a mouse click.
    /// Prefers the innermost slot under the pointer, so clicking a denominator lands in the denominator.
    /// </summary>
    public (MathRow Row, int Index)? HitTest(Point p)
    {
        if (_hits.Count == 0) return null;
        double x = (p.X - _ox) / _scale, y = (p.Y - _oy) / _scale;
        Hit? best = null;
        double bestScore = double.MaxValue;
        foreach (var h in _hits)
        {
            double dy = y < h.Top ? h.Top - y : y > h.Bottom ? y - h.Bottom : 0;
            double score = Math.Abs(x - h.X) + dy * 3 + (h.Bottom - h.Top) * 0.05;
            if (score < bestScore) { bestScore = score; best = h; }
        }
        return best is { } b ? (b.Row, b.Index) : null;
    }

    private readonly record struct Hit(MathRow Row, int Index, double X, double Top, double Bottom);

    // ================= layout engine =================

    private sealed class DrawCtx(DrawingContext dc, IBrush fg, IBrush cursor, MathRow? cursorRow, int cursorIndex, bool showCursor)
    {
        public DrawingContext Dc = dc;
        public IBrush Fg = fg;
        public IBrush Cursor = cursor;
        public MathRow? CursorRow = cursorRow;
        public int CursorIndex = cursorIndex;
        public bool ShowCursor = showCursor;
        public List<Hit> Hits = new();
        private IBrush? _faint;
        public IBrush Faint => _faint ??= Fade(Fg, 0.28);

        private static IBrush Fade(IBrush b, double o) =>
            b is ISolidColorBrush s ? new SolidColorBrush(s.Color, o) : b;
    }

    /// <summary>W = width, A = height above the math axis, D = depth below it.</summary>
    private abstract class Box
    {
        public double W, A, D;
        public abstract void Draw(DrawCtx c, double x, double axis);
    }

    private sealed class LayoutCtx(Typeface face, MathRow? cursorRow)
    {
        public Typeface Face = face;
        public MathRow? CursorRow = cursorRow;

        public FormattedText Text(string s, double size) =>
            new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Face, size, Brushes.White);

        public Box Row(MathRow row, double size, bool isTop = false)
        {
            var rb = new RowBox(row, size);
            Box? prev = null;
            foreach (var n in row.Items)
            {
                var b = Node(n, size, prev);
                rb.Children.Add(b);
                prev = b;
            }
            if (row.Items.Count == 0)
            {
                if (isTop) rb.Placeholder = new TextBox(this, "0", size, 0);
                else rb.Empty = true;
            }
            rb.Measure();
            return rb;
        }

        private double Smaller(double size, double f) => Math.Max(9, size * f);

        private Box Node(Node n, double s, Box? prev) => n switch
        {
            Glyph g => new TextBox(this, g.Text, s, g.Kind == GlyphKind.Operator ? s * 0.16 : g.Kind == GlyphKind.Function ? s * 0.03 : 0.0),
            Fraction f => new FracBox(Row(f.Num, Smaller(s, 0.8)), Row(f.Den, Smaller(s, 0.8)), s),
            Power p => new PowBox(Row(p.Exp, Smaller(s, 0.62)), s, prev),
            Root r => new RootBox(r.Square ? null : Row(r.Index, Smaller(s, 0.5)), Row(r.Radicand, s), s),
            Abs a => new AbsBox(Row(a.Inner, s), s),
            LogBase l => new LogBox(this, Row(l.Base, Smaller(s, 0.62)), Row(l.Arg, s), s),
            _ => new TextBox(this, "?", s, 0),
        };
    }

    /// <summary>
    /// Plain text in the app font. Superscript/subscript characters (⁻¹ ² ³ ⁿ ˣ ʸ ₂ ₙ) are drawn as small
    /// raised/lowered normal characters, so they never fall back to a different font.
    /// </summary>
    private sealed class TextBox : Box
    {
        private readonly List<(FormattedText Ft, double Dx, double BaseShift)> _runs = new();
        private readonly double _pad, _baseline;

        private static readonly Dictionary<char, char> Sup = new()
        { ['⁻'] = '−', ['¹'] = '1', ['²'] = '2', ['³'] = '3', ['ⁿ'] = 'n', ['ˣ'] = 'x', ['ʸ'] = 'y' };
        private static readonly Dictionary<char, char> Sub = new() { ['₂'] = '2', ['ₙ'] = 'n' };

        public TextBox(LayoutCtx c, string text, double size, double pad)
        {
            _pad = pad;
            var main = c.Text("0", size);
            _baseline = main.Baseline;
            double x = 0, top = 0, bottom = main.Height;
            int i = 0;
            while (i < text.Length)
            {
                int kind = Sup.ContainsKey(text[i]) ? 1 : Sub.ContainsKey(text[i]) ? 2 : 0;
                var sb = new System.Text.StringBuilder();
                while (i < text.Length)
                {
                    int k = Sup.ContainsKey(text[i]) ? 1 : Sub.ContainsKey(text[i]) ? 2 : 0;
                    if (k != kind) break;
                    sb.Append(k == 1 ? Sup[text[i]] : k == 2 ? Sub[text[i]] : text[i]);
                    i++;
                }
                double fs = kind == 0 ? size : size * 0.62;
                var ft = c.Text(sb.ToString(), fs);
                double shift = kind == 1 ? -size * 0.42 : kind == 2 ? size * 0.2 : 0; // baseline offset
                double runTop = _baseline + shift - ft.Baseline;
                top = Math.Min(top, runTop);
                bottom = Math.Max(bottom, runTop + ft.Height);
                _runs.Add((ft, x, shift));
                x += ft.WidthIncludingTrailingWhitespace + (kind == 1 ? size * 0.02 : 0);
            }
            W = x + pad * 2;
            // Math axis (height of a minus sign) sits about 0.27em above the baseline.
            double axisY = _baseline - size * 0.27;
            A = axisY - top;
            D = bottom - axisY;
            _axisFromTop = axisY;
        }

        private readonly double _axisFromTop;

        public override void Draw(DrawCtx c, double x, double axis)
        {
            double baselineY = axis - _axisFromTop + _baseline;
            foreach (var (ft, dx, shift) in _runs)
            {
                ft.SetForegroundBrush(c.Fg);
                c.Dc.DrawText(ft, new Point(x + _pad + dx, baselineY + shift - ft.Baseline));
            }
        }
    }

    private sealed class RowBox(MathRow row, double size) : Box
    {
        public readonly List<Box> Children = new();
        public Box? Placeholder;
        public bool Empty;
        private readonly MathRow _row = row;
        private readonly double _size = size;

        public void Measure()
        {
            if (Placeholder != null) { W = Placeholder.W; A = Placeholder.A; D = Placeholder.D; return; }
            if (Empty) { W = _size * 0.75; A = _size * 0.36; D = _size * 0.30; return; }
            W = 0; A = _size * 0.5; D = _size * 0.3;
            foreach (var b in Children) { W += b.W; A = Math.Max(A, b.A); D = Math.Max(D, b.D); }
        }

        public override void Draw(DrawCtx c, double x, double axis)
        {
            if (Placeholder != null)
            {
                c.Hits.Add(new Hit(_row, 0, x + W, axis - A, axis + D));
                Placeholder.Draw(c, x, axis);
                if (c.ShowCursor && c.CursorRow == _row) Caret(c, x + W + _size * 0.04, axis);
                return;
            }
            if (Empty)
            {
                bool here = c.CursorRow == _row;
                c.Hits.Add(new Hit(_row, 0, x + W / 2, axis - A, axis + D));
                var rect = new Rect(x + _size * 0.06, axis - A, W - _size * 0.12, A + D);
                c.Dc.DrawRectangle(here ? null : c.Faint, here ? new Pen(c.Cursor, Math.Max(1, _size * 0.05)) : null, rect, _size * 0.08, _size * 0.08);
                if (here && c.ShowCursor) Caret(c, x + W / 2, axis);
                return;
            }
            double cx = x;
            for (int i = 0; i < Children.Count; i++)
            {
                c.Hits.Add(new Hit(_row, i, cx, axis - A, axis + D));
                if (c.ShowCursor && c.CursorRow == _row && c.CursorIndex == i) Caret(c, cx, axis);
                Children[i].Draw(c, cx, axis);
                cx += Children[i].W;
            }
            c.Hits.Add(new Hit(_row, Children.Count, cx, axis - A, axis + D));
            if (c.ShowCursor && c.CursorRow == _row && c.CursorIndex >= Children.Count) Caret(c, cx, axis);
        }

        private void Caret(DrawCtx c, double x, double axis)
        {
            double t = Math.Max(1.5, _size * 0.055);
            c.Dc.FillRectangle(c.Cursor, new Rect(x - t / 2, axis - _size * 0.62, t, _size * 0.98), (float)(t / 2));
        }
    }

    private sealed class FracBox : Box
    {
        private readonly Box _n, _d;
        private readonly double _th, _gap, _pad;
        public FracBox(Box n, Box d, double s)
        {
            _n = n; _d = d;
            _th = Math.Max(1, s * 0.055);
            _gap = s * 0.1;
            _pad = s * 0.1;
            W = Math.Max(n.W, d.W) + _pad * 2;
            A = n.A + n.D + _gap + _th / 2;
            D = d.A + d.D + _gap + _th / 2;
        }
        public override void Draw(DrawCtx c, double x, double axis)
        {
            c.Dc.FillRectangle(c.Fg, new Rect(x + _pad * 0.4, axis - _th / 2, W - _pad * 0.8, _th));
            _n.Draw(c, x + (W - _n.W) / 2, axis - _th / 2 - _gap - _n.D);
            _d.Draw(c, x + (W - _d.W) / 2, axis + _th / 2 + _gap + _d.A);
        }
    }

    private sealed class PowBox : Box
    {
        private readonly Box _e;
        private readonly double _raise;
        public PowBox(Box e, double s, Box? prev)
        {
            _e = e;
            double prevTop = prev?.A ?? s * 0.5;
            _raise = prev is TextBox or null
                ? s * 0.30 + e.D * 0.55
                : Math.Max(s * 0.30, prevTop - s * 0.3) + e.D * 0.55;
            W = e.W + s * 0.04;
            A = _raise + e.A;
            D = Math.Max(0, e.D - _raise);
        }
        public override void Draw(DrawCtx c, double x, double axis) => _e.Draw(c, x + 1, axis - _raise);
    }

    private sealed class RootBox : Box
    {
        private readonly Box? _i;
        private readonly Box _r;
        private readonly double _s, _sw, _th, _ix, _radA;
        public RootBox(Box? index, Box radicand, double s)
        {
            _i = index; _r = radicand; _s = s;
            _sw = s * 0.58;
            _th = Math.Max(1.2, s * 0.055);
            double gapTop = s * 0.12;
            _radA = radicand.A + gapTop + _th;
            D = radicand.D + s * 0.06;
            A = _radA;
            _ix = 0;
            if (index != null)
            {
                _ix = Math.Max(0, index.W - _sw * 0.42);
                double h = _radA + D;
                double tickY = -_radA + h * 0.52;             // relative to axis
                double indexTop = tickY - s * 0.06 - (index.A + index.D);
                A = Math.Max(A, -indexTop);
            }
            W = _ix + _sw + radicand.W + s * 0.12;
        }
        public override void Draw(DrawCtx c, double x, double axis)
        {
            double top = axis - _radA, bottom = axis + D, h = bottom - top;
            double x0 = x + _ix;
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(new Point(x0 + _sw * 0.02, top + h * 0.6), false);
                ctx.LineTo(new Point(x0 + _sw * 0.22, top + h * 0.52));
                ctx.LineTo(new Point(x0 + _sw * 0.5, bottom - _th));
                ctx.LineTo(new Point(x0 + _sw * 0.92, top + _th / 2));
                ctx.LineTo(new Point(x + W - _s * 0.02, top + _th / 2));
                ctx.EndFigure(false);
            }
            c.Dc.DrawGeometry(null, new Pen(c.Fg, _th, lineJoin: PenLineJoin.Round, lineCap: PenLineCap.Round), g);
            if (_i != null)
            {
                double tickY = top + h * 0.52;
                _i.Draw(c, x0 + _sw * 0.42 - _i.W, tickY - _s * 0.06 - _i.D);
            }
            _r.Draw(c, x0 + _sw + _s * 0.04, axis);
        }
    }

    private sealed class AbsBox : Box
    {
        private readonly Box _in;
        private readonly double _s, _th;
        public AbsBox(Box inner, double s)
        {
            _in = inner; _s = s; _th = Math.Max(1.2, s * 0.05);
            W = inner.W + s * 0.56; A = inner.A + s * 0.06; D = inner.D + s * 0.06;
        }
        public override void Draw(DrawCtx c, double x, double axis)
        {
            c.Dc.FillRectangle(c.Fg, new Rect(x + _s * 0.08, axis - A, _th, A + D));
            c.Dc.FillRectangle(c.Fg, new Rect(x + W - _s * 0.08 - _th, axis - A, _th, A + D));
            _in.Draw(c, x + _s * 0.28, axis);
        }
    }

    private sealed class LogBox : Box
    {
        private readonly TextBox _log;
        private readonly Box _b, _arg;
        private readonly FormattedText _lp, _rp;
        private readonly double _s, _drop, _pw, _parenTop;
        public LogBox(LayoutCtx c, Box b, Box arg, double s)
        {
            _s = s; _b = b; _arg = arg;
            _log = new TextBox(c, "log", s, s * 0.03);
            _drop = s * 0.32 + b.A * 0.4;
            double inner = Math.Max(arg.A, s * 0.5) + Math.Max(arg.D, s * 0.3);
            double ps = Math.Max(s, inner / 1.05);
            _lp = c.Text("(", ps);
            _rp = c.Text(")", ps);
            _pw = _lp.WidthIncludingTrailingWhitespace;
            _parenTop = _lp.Baseline - ps * 0.27;
            W = _log.W + b.W + _pw * 2 + arg.W + s * 0.04;
            A = Math.Max(Math.Max(_log.A, arg.A), _parenTop);
            D = Math.Max(Math.Max(_log.D, b.D + _drop), Math.Max(arg.D, _lp.Height - _parenTop));
        }
        public override void Draw(DrawCtx c, double x, double axis)
        {
            _log.Draw(c, x, axis);
            double cx = x + _log.W;
            _b.Draw(c, cx, axis + _drop);
            cx += _b.W + _s * 0.04;
            _lp.SetForegroundBrush(c.Fg); _rp.SetForegroundBrush(c.Fg);
            c.Dc.DrawText(_lp, new Point(cx, axis - _parenTop));
            cx += _pw;
            _arg.Draw(c, cx, axis);
            cx += _arg.W;
            c.Dc.DrawText(_rp, new Point(cx, axis - _parenTop));
        }
    }
}
