using System.Text;

namespace Calculator.MathCore;

/// <summary>A horizontal run of math items, e.g. the numerator of a fraction.</summary>
public sealed class MathRow
{
    public List<Node> Items { get; } = new();
    /// <summary>The structure this row is a slot of (null for the top-level row).</summary>
    public Node? Owner { get; set; }
    public MathRow? ParentRow => Owner?.Parent;

    public bool IsEmpty => Items.Count == 0;

    public void Insert(int index, Node n)
    {
        n.Parent = this;
        Items.Insert(index, n);
    }

    public string ToEval()
    {
        var sb = new StringBuilder();
        foreach (var n in Items) sb.Append(n.ToEval());
        return sb.ToString();
    }

    public MathRow CloneDeep(Node? owner = null)
    {
        var r = new MathRow { Owner = owner };
        foreach (var n in Items) r.Insert(r.Items.Count, n.CloneDeep());
        return r;
    }
}

public abstract class Node
{
    public MathRow? Parent { get; set; }
    /// <summary>Editable slots (rows) inside this node, in left-to-right/top-to-bottom order.</summary>
    public virtual IReadOnlyList<MathRow> Slots => Array.Empty<MathRow>();
    public abstract string ToEval();
    public abstract Node CloneDeep();

    protected MathRow NewSlot() => new() { Owner = this };
    protected MathRow CopySlot(MathRow r) => r.CloneDeep(this);
}

/// <summary>A single displayed token: a digit, operator, function name, constant.</summary>
public sealed class Glyph : Node
{
    public Glyph(string text, string eval, GlyphKind kind = GlyphKind.Symbol)
    {
        Text = text; Eval = eval; Kind = kind;
    }

    public string Text { get; }
    public string Eval { get; }
    public GlyphKind Kind { get; }

    public bool IsNumberPart => Kind == GlyphKind.Digit;
    public override string ToEval() => Eval;
    public override Node CloneDeep() => new Glyph(Text, Eval, Kind);
}

public enum GlyphKind { Digit, Operator, Function, Symbol }

/// <summary>A stacked fraction: numerator over denominator.</summary>
public sealed class Fraction : Node
{
    public Fraction() { Num = NewSlot(); Den = NewSlot(); }
    public MathRow Num { get; private set; }
    public MathRow Den { get; private set; }
    public override IReadOnlyList<MathRow> Slots => new[] { Num, Den };
    public override string ToEval() => $"(({Num.ToEval()})/({Den.ToEval()}))";
    public override Node CloneDeep() { var f = new Fraction(); f.Num = f.CopySlot(Num); f.Den = f.CopySlot(Den); return f; }
}

/// <summary>A raised exponent that applies to whatever stands to its left.</summary>
public sealed class Power : Node
{
    public Power() { Exp = NewSlot(); }
    public MathRow Exp { get; private set; }
    public override IReadOnlyList<MathRow> Slots => new[] { Exp };
    public override string ToEval() => $"^({Exp.ToEval()})";
    public override Node CloneDeep() { var p = new Power(); p.Exp = p.CopySlot(Exp); return p; }
}

/// <summary>A small subscript-free radical. Square roots have no index slot.</summary>
public sealed class Root : Node
{
    public Root(bool square) { Square = square; Index = NewSlot(); Radicand = NewSlot(); }
    public bool Square { get; }
    public MathRow Index { get; private set; }
    public MathRow Radicand { get; private set; }
    public override IReadOnlyList<MathRow> Slots => Square ? new[] { Radicand } : new[] { Index, Radicand };
    public override string ToEval() => Square ? $"sqrt({Radicand.ToEval()})" : $"root(({Index.ToEval()}),({Radicand.ToEval()}))";
    public override Node CloneDeep() { var r = new Root(Square); r.Index = r.CopySlot(Index); r.Radicand = r.CopySlot(Radicand); return r; }
}

/// <summary>|x|</summary>
public sealed class Abs : Node
{
    public Abs() { Inner = NewSlot(); }
    public MathRow Inner { get; private set; }
    public override IReadOnlyList<MathRow> Slots => new[] { Inner };
    public override string ToEval() => $"abs({Inner.ToEval()})";
    public override Node CloneDeep() { var a = new Abs(); a.Inner = a.CopySlot(Inner); return a; }
}

/// <summary>log with a written base: log_b(x).</summary>
public sealed class LogBase : Node
{
    public LogBase() { Base = NewSlot(); Arg = NewSlot(); }
    public MathRow Base { get; private set; }
    public MathRow Arg { get; private set; }
    public override IReadOnlyList<MathRow> Slots => new[] { Base, Arg };
    public override string ToEval() => $"logb(({Base.ToEval()}),({Arg.ToEval()}))";
    public override Node CloneDeep() { var l = new LogBase(); l.Base = l.CopySlot(Base); l.Arg = l.CopySlot(Arg); return l; }
}
