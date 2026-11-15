using System.Globalization;
using Calculator.MathCore;

namespace Calculator;

/// <summary>Calculator state: the editor, the last answer, memory, and what each command does.</summary>
public sealed class CalcEngine
{
    public Editor Editor { get; } = new();
    public Evaluator Eval { get; } = new();

    /// <summary>The expression shown above the result after "=".</summary>
    public MathRow? LastExpression { get; private set; }
    public double? Result { get; private set; }
    public string? Error { get; private set; }
    public bool ShowingResult { get; private set; }
    /// <summary>Default: show results as fractions when the expression used a fraction.</summary>
    public bool FractionMode { get; set; } = true;
    /// <summary>Whether the current result/preview is drawn as a fraction (S↔D flips it).</summary>
    public bool ResultAsFraction { get; private set; }
    public double Memory { get; private set; }
    public bool HasMemory { get; private set; }

    public event Action? Changed;

    public History History { get; } = History.Load();

    /// <summary>Puts the cursor where the user clicked in the display.</summary>
    public void PlaceCursor(MathRow row, int index)
    {
        if (ShowingResult) return;
        Editor.SetCursor(row, index);
        Changed?.Invoke();
    }

    /// <summary>Shows a calculation from the history again; typing an operator continues from its result.</summary>
    public void Recall(HistoryEntry e)
    {
        LastExpression = e.Expression.CloneDeep();
        Result = e.Value;
        Eval.Ans = e.Value;
        Error = null;
        ShowingResult = true;
        ResultAsFraction = FractionMode && ContainsFraction(LastExpression);
        Changed?.Invoke();
    }

    /// <summary>Inserts a past result into the expression being typed.</summary>
    public void InsertValue(double v)
    {
        BeginInput(false);
        InsertNumber(v);
        SyncPreviewFormat();
        Changed?.Invoke();
    }

    private static Glyph Digit(string d) => new(d, d, GlyphKind.Digit);
    private static Glyph Op(string text, string eval) => new(text, eval, GlyphKind.Operator);
    private static Glyph Fn(string text, string eval) => new(text + "(", eval + "(", GlyphKind.Function);
    private static Glyph Sym(string text, string eval) => new(text, eval, GlyphKind.Symbol);
    private static Glyph Close() => new(")", ")", GlyphKind.Symbol);

    /// <summary>Live preview of the current expression (null when it isn't complete yet).</summary>
    public double? Preview()
    {
        if (ShowingResult || Editor.IsEmpty) return null;
        try { return Eval.Evaluate(Editor.ToEval()); } catch { return null; }
    }

    /// <summary>True when the expression is just a plain number (no point previewing "= 5" under "5").</summary>
    public bool ExpressionIsPlainNumber => Editor.Root.Items.All(n => n is Glyph { Kind: GlyphKind.Digit });

    // Called before typing anything after "=": numbers start fresh, operators continue from Ans.
    private void BeginInput(bool continuesFromAnswer)
    {
        Error = null;
        if (!ShowingResult) return;
        ShowingResult = false;
        Editor.Clear();
        if (continuesFromAnswer && Result is not null) Editor.Insert(Sym("Ans", "Ans"));
    }

    public void Run(string id)
    {
        switch (id)
        {
            case var d when d.Length == 2 && d[0] == 'd' && char.IsDigit(d[1]):
                BeginInput(false); Editor.Insert(Digit(d[1..])); break;
            case "dot": BeginInput(false); Editor.Insert(Digit(".")); break;

            case "add": BeginInput(true); Editor.Insert(Op("+", "+")); break;
            case "sub": BeginInput(true); Editor.Insert(Op("−", "-")); break;
            case "mul": BeginInput(true); Editor.Insert(Op("×", "*")); break;
            case "div": BeginInput(true); Editor.Insert(Op("÷", "/")); break;
            case "mod": BeginInput(true); Editor.Insert(Op("mod", "mod")); break;
            case "ncr": BeginInput(true); Editor.Insert(Op("C", "nCr")); break;
            case "npr": BeginInput(true); Editor.Insert(Op("P", "nPr")); break;
            case "percent": BeginInput(true); Editor.Insert(Sym("%", "%")); break;
            case "fact": BeginInput(true); Editor.Insert(Sym("!", "!")); break;
            case "neg": Negate(); break;

            case "lparen": BeginInput(false); Editor.Insert(Sym("(", "(")); break;
            case "rparen": BeginInput(true); Editor.Insert(Close()); break;

            case "frac": BeginInput(true); Editor.InsertFraction(); break;
            case "sqrt": BeginInput(false); Editor.InsertStructure(new Root(square: true)); break;
            case "cbrt":
            {
                BeginInput(false);
                var r = new Root(square: false);
                r.Index.Insert(0, Digit("3"));
                Editor.InsertStructure(r, focusSlot: 1);
                break;
            }
            case "nroot": BeginInput(false); Editor.InsertStructure(new Root(square: false), focusSlot: 0); break;
            case "pow": BeginInput(true); Editor.InsertStructure(new Power()); break;
            case "square": InsertFixedPower("2"); break;
            case "cube": InsertFixedPower("3"); break;
            case "inv":
            {
                BeginInput(true);
                if (Editor.HasOperandBefore)
                {
                    var p = new Power();
                    p.Exp.Insert(0, Op("−", "-"));
                    p.Exp.Insert(1, Digit("1"));
                    InsertNode(p);
                }
                else
                {
                    var f = new Fraction();
                    f.Num.Insert(0, Digit("1"));
                    Editor.InsertStructure(f, focusSlot: 1);
                }
                break;
            }
            case "exp": BeginInput(false); Editor.Insert(Sym("e", "ℯ")); Editor.InsertStructure(new Power()); break;
            case "pow10": BeginInput(false); Editor.Insert(Digit("1")); Editor.Insert(Digit("0")); Editor.InsertStructure(new Power()); break;
            case "pow2": BeginInput(false); Editor.Insert(Digit("2")); Editor.InsertStructure(new Power()); break;
            case "ee": BeginInput(true); Editor.Insert(new Glyph("E", "E", GlyphKind.Digit)); break;
            case "abs":
            {
                BeginInput(false);
                Editor.InsertStructure(new Abs());
                break;
            }

            case "ln": Func("ln", "ln"); break;
            case "log": Func("log", "log"); break;
            case "log2": Func("log₂", "lbin"); break;
            case "logb": BeginInput(false); Editor.InsertStructure(new LogBase()); break;
            case "sin" or "cos" or "tan" or "sinh" or "cosh" or "tanh" or "sec" or "csc" or "cot":
                Func(id, id); break;
            case "asin": Func("sin⁻¹", "asin"); break;
            case "acos": Func("cos⁻¹", "acos"); break;
            case "atan": Func("tan⁻¹", "atan"); break;
            case "asinh": Func("sinh⁻¹", "asinh"); break;
            case "acosh": Func("cosh⁻¹", "acosh"); break;
            case "atanh": Func("tanh⁻¹", "atanh"); break;
            case "floor": Func("floor", "floor"); break;
            case "ceil": Func("ceil", "ceil"); break;
            case "round": Func("round", "round"); break;

            case "pi": BeginInput(false); Editor.Insert(Sym("π", "π")); break;
            case "e": BeginInput(false); Editor.Insert(Sym("e", "ℯ")); break;
            case "ans": BeginInput(false); Editor.Insert(Sym("Ans", "Ans")); break;
            case "rand":
            {
                BeginInput(false);
                var v = Random.Shared.NextDouble().ToString("0.##########", CultureInfo.InvariantCulture);
                foreach (var c in v) Editor.Insert(Digit(c.ToString()));
                break;
            }

            case "angle": Eval.Degrees = !Eval.Degrees; if (ShowingResult && LastExpression != null) Recalculate(); break;
            case "sd": ResultAsFraction = !ResultAsFraction; break;

            case "mc": Memory = 0; HasMemory = false; break;
            case "mplus": if (CurrentValue() is { } a) { Memory += a; HasMemory = true; } break;
            case "mminus": if (CurrentValue() is { } b) { Memory -= b; HasMemory = true; } break;
            case "mr":
            {
                if (!HasMemory) break;
                BeginInput(false);
                InsertNumber(Memory);
                break;
            }

            case "clear": Editor.Clear(); ShowingResult = false; LastExpression = null; Error = null; Result = null; break;
            case "back":
                if (ShowingResult) { ShowingResult = false; Editor.Load(LastExpression ?? new MathRow()); }
                else Editor.Backspace();
                Error = null;
                break;
            case "del": if (!ShowingResult) Editor.Delete(); break;
            case "left": Unfreeze(); Editor.MoveLeft(); break;
            case "right": Unfreeze(); Editor.MoveRight(); break;
            case "up": Unfreeze(); Editor.MoveUp(); break;
            case "down": Unfreeze(); Editor.MoveDown(); break;
            case "home": Unfreeze(); Editor.Home(); break;
            case "end": Unfreeze(); Editor.End(); break;

            case "eq": Equals(); break;
            default: return;
        }
        if (id != "sd") SyncPreviewFormat();
        Changed?.Invoke();
    }

    /// <summary>Types a plain character from the keyboard. Returns false if it means nothing.</summary>
    public bool Type(char c)
    {
        string? id = c switch
        {
            >= '0' and <= '9' => "d" + c,
            '.' or ',' => "dot",
            '+' => "add",
            '-' or '−' => "sub",
            '*' or '×' or 'x' => "mul",
            '/' or '÷' => "div",
            '^' => "pow",
            '(' or '[' => "lparen",
            ')' or ']' => "rparen",
            '!' => "fact",
            '%' => "percent",
            '=' => "eq",
            '|' => "abs",
            'π' => "pi",
            _ => null,
        };
        if (id == null) return false;
        Run(id);
        return true;
    }

    public void PasteText(string text)
    {
        text = text.Trim().Replace(" ", "").Replace(" ", "");
        if (text.Length == 0 || text.Length > 200) return;
        // Accept things like "1,234.5" or "1234,5": a lone comma is a decimal separator.
        if (text.Contains(',') && !text.Contains('.') && text.Count(ch => ch == ',') == 1) text = text.Replace(',', '.');
        else text = text.Replace(",", "");
        foreach (var c in text)
        {
            if (c is 'e' or 'E') { Run("ee"); continue; }
            Type(c);
        }
    }

    public string? CopyText()
    {
        double? v = ShowingResult ? Result : Preview();
        return v is { } d ? Formatter.Text(d) : null;
    }

    private void Unfreeze()
    {
        if (!ShowingResult) return;
        ShowingResult = false;
        Editor.Load(LastExpression ?? new MathRow());
    }

    private double? CurrentValue() => ShowingResult ? Result : Preview();

    private void Func(string text, string eval)
    {
        BeginInput(false);
        Editor.Insert(Fn(text, eval));
    }

    private void InsertFixedPower(string exponent)
    {
        BeginInput(true);
        var p = new Power();
        p.Exp.Insert(0, Digit(exponent));
        InsertNode(p);
    }

    private void InsertNode(Node n)
    {
        // Insert a finished structure and leave the cursor after it.
        Editor.InsertStructure(n);
        Editor.SetCursor(n.Parent!, n.Parent!.Items.IndexOf(n) + 1);
    }

    private void InsertNumber(double v)
    {
        var s = Formatter.Text(v).Replace("E+", "E");
        if (v < 0) { Editor.Insert(Op("−", "-")); s = s.TrimStart('-'); }
        foreach (var c in s)
        {
            if (c == 'E') Editor.Insert(new Glyph("E", "E", GlyphKind.Digit));
            else if (c == '-') Editor.Insert(new Glyph("−", "-", GlyphKind.Digit));
            else Editor.Insert(Digit(c.ToString()));
        }
    }

    private void Negate()
    {
        if (ShowingResult && Result is { } r)
        {
            ShowingResult = false;
            Editor.Clear();
            InsertNumber(-r);
            return;
        }
        // Toggle a minus sign in front of the number left of the cursor.
        var row = Editor.Row;
        int i = Editor.Index;
        while (i > 0 && row.Items[i - 1] is Glyph { Kind: GlyphKind.Digit }) i--;
        if (i > 0 && row.Items[i - 1] is Glyph { Text: "−" } minus &&
            (i == 1 || row.Items[i - 2] is Glyph { Kind: GlyphKind.Operator } || row.Items[i - 2] is Glyph { Text: "(" }))
        {
            row.Items.Remove(minus);
            Editor.SetCursor(row, Editor.Index - 1);
        }
        else
        {
            row.Insert(i, new Glyph("−", "-", GlyphKind.Operator));
            Editor.SetCursor(row, Editor.Index + 1);
        }
    }

    private void Equals()
    {
        if (ShowingResult) { Recalculate(); return; }
        if (Editor.IsEmpty) return;
        var expr = Editor.Root.CloneDeep();
        try
        {
            var v = Eval.Evaluate(Editor.ToEval());
            Result = v;
            Eval.Ans = v;
            Error = null;
            LastExpression = expr;
            ShowingResult = true;
            ResultAsFraction = FractionMode && ContainsFraction(expr);
            History.Add(expr, v);
        }
        catch (CalcException ex) { Error = ex.Message; }
        catch (Exception) { Error = "Error"; }
    }

    // Fraction display only makes sense for exact arithmetic: fractions without logs, roots, trig, π, e,
    // decimal powers or other functions, which give irrational results that only look like fractions.
    private static bool ContainsFraction(MathRow row) => HasFraction(row) && IsExact(row);

    private static bool HasFraction(MathRow row) =>
        row.Items.Any(n => n is Fraction || n.Slots.Any(HasFraction));

    private static bool IsExact(MathRow row) => row.Items.All(n => n switch
    {
        Glyph g => g.Kind != GlyphKind.Function && g.Eval is not ("π" or "ℯ" or "Ans" or "E" or "%"),
        MathCore.Root or LogBase => false,
        Power p => p.Exp.Items.All(i => i is Glyph { Kind: GlyphKind.Digit or GlyphKind.Operator } g2 && g2.Text != "."),
        _ => n.Slots.All(IsExact),
    });

    /// <summary>Keeps the preview's fraction/decimal look in step with what's being typed.</summary>
    public void SyncPreviewFormat()
    {
        if (!ShowingResult) ResultAsFraction = FractionMode && ContainsFraction(Editor.Root);
    }

    private void Recalculate()
    {
        if (LastExpression == null) return;
        try
        {
            var probe = new Editor();
            probe.Load(LastExpression);
            var v = Eval.Evaluate(probe.ToEval());
            Result = v; Eval.Ans = v; Error = null;
        }
        catch (CalcException ex) { Error = ex.Message; }
    }
}
