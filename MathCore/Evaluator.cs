using System.Globalization;

namespace Calculator.MathCore;

public sealed class CalcException(string message) : Exception(message);

/// <summary>Recursive-descent evaluator for the editor's evaluation string.</summary>
public sealed class Evaluator
{
    public bool Degrees { get; set; } = true;
    public double Ans { get; set; }

    private List<Tok> _t = new();
    private int _p;

    private enum K { Num, Ident, Op, LParen, RParen, Comma, End }
    private readonly record struct Tok(K Kind, string Text, double Value = 0);

    public double Evaluate(string src)
    {
        _t = Tokenize(src);
        _p = 0;
        if (_t.Count == 1) throw new CalcException("Empty");
        var v = Expr();
        while (Peek.Kind == K.RParen) _p++; // tolerate extra closing brackets
        if (Peek.Kind != K.End) throw new CalcException("Syntax error");
        if (double.IsNaN(v)) throw new CalcException("Not a number");
        return v;
    }

    // ---------- tokens ----------

    private static List<Tok> Tokenize(string s)
    {
        var list = new List<Tok>();
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (char.IsDigit(c) || c == '.')
            {
                int st = i;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
                if (i < s.Length && s[i] == 'E')
                {
                    int save = i;
                    i++;
                    if (i < s.Length && (s[i] == '-' || s[i] == '+')) i++;
                    if (i < s.Length && char.IsDigit(s[i])) { while (i < s.Length && char.IsDigit(s[i])) i++; }
                    else i = save;
                }
                var text = s[st..i];
                if (text.Count(ch => ch == '.') > 1 || text == ".")
                    throw new CalcException("Bad number");
                list.Add(new Tok(K.Num, text, double.Parse(text, CultureInfo.InvariantCulture)));
                continue;
            }
            if (char.IsLetter(c) && c != 'π' && c != 'ℯ')
            {
                int st = i;
                while (i < s.Length && char.IsLetter(s[i]) && s[i] != 'π' && s[i] != 'ℯ') i++;
                var word = s[st..i];
                if (word is "mod" or "nCr" or "nPr") list.Add(new Tok(K.Op, word));
                else list.Add(new Tok(K.Ident, word));
                continue;
            }
            switch (c)
            {
                case 'π': list.Add(new Tok(K.Num, "π", Math.PI)); break;
                case 'ℯ': list.Add(new Tok(K.Num, "e", Math.E)); break;
                case '(': list.Add(new Tok(K.LParen, "(")); break;
                case ')': list.Add(new Tok(K.RParen, ")")); break;
                case ',': list.Add(new Tok(K.Comma, ",")); break;
                case '+' or '-' or '*' or '/' or '^' or '!' or '%': list.Add(new Tok(K.Op, c.ToString())); break;
                default: throw new CalcException($"Unexpected '{c}'");
            }
            i++;
        }
        list.Add(new Tok(K.End, ""));
        return list;
    }

    private Tok Peek => _t[_p];
    private Tok Next() => _t[_p++];
    private bool IsOp(string op) => Peek.Kind == K.Op && Peek.Text == op;

    // ---------- grammar ----------

    private double Expr()
    {
        double v = Term();
        while (true)
        {
            if (IsOp("+")) { _p++; v += Term(); }
            else if (IsOp("-")) { _p++; v -= Term(); }
            else return v;
        }
    }

    private bool StartsPrimary => Peek.Kind is K.Num or K.Ident or K.LParen;

    private double Term()
    {
        double v = Unary();
        while (true)
        {
            if (IsOp("*")) { _p++; v *= Unary(); }
            else if (IsOp("/"))
            {
                _p++;
                double d = Unary();
                if (d == 0) throw new CalcException("Can't divide by 0");
                v /= d;
            }
            else if (IsOp("mod"))
            {
                _p++;
                double d = Unary();
                if (d == 0) throw new CalcException("Can't divide by 0");
                v -= d * Math.Floor(v / d);
            }
            else if (IsOp("nCr")) { _p++; v = Combinations(v, Unary()); }
            else if (IsOp("nPr")) { _p++; v = Permutations(v, Unary()); }
            else if (StartsPrimary) v *= Power(); // implicit multiplication: 2π, 3(4), 2√9
            else return v;
        }
    }

    private double Unary()
    {
        if (IsOp("-")) { _p++; return -Unary(); }
        if (IsOp("+")) { _p++; return Unary(); }
        return Power();
    }

    private double Power()
    {
        double b = Postfix();
        if (IsOp("^"))
        {
            _p++;
            double e = Unary(); // right-associative
            return Pow(b, e);
        }
        return b;
    }

    private double Postfix()
    {
        double v = Primary();
        while (true)
        {
            if (IsOp("!")) { _p++; v = Factorial(v); }
            else if (IsOp("%")) { _p++; v /= 100; }
            else if (IsOp("^") && _t[_p + 1].Kind == K.LParen) return v; // handled in Power
            else return v;
        }
    }

    private double Primary()
    {
        var t = Next();
        switch (t.Kind)
        {
            case K.Num: return t.Value;
            case K.LParen:
            {
                double v = Expr();
                if (Peek.Kind == K.RParen) _p++;
                else if (Peek.Kind != K.End) throw new CalcException("Missing )");
                return v;
            }
            case K.Ident:
            {
                if (t.Text == "Ans") return Ans;
                if (Peek.Kind != K.LParen) throw new CalcException($"Unknown '{t.Text}'");
                _p++;
                var args = new List<double> { Expr() };
                while (Peek.Kind == K.Comma) { _p++; args.Add(Expr()); }
                if (Peek.Kind == K.RParen) _p++;
                else if (Peek.Kind != K.End) throw new CalcException("Missing )");
                return Call(t.Text, args);
            }
            case K.End: throw new CalcException("Incomplete");
            default: throw new CalcException("Syntax error");
        }
    }

    // ---------- functions ----------

    private double ToRad(double x) => Degrees ? x * Math.PI / 180 : x;
    private double FromRad(double x) => Degrees ? x * 180 / Math.PI : x;

    private static double Snap(double x) => Math.Abs(x) < 1e-15 ? 0 : x;

    private double Call(string name, List<double> a)
    {
        double x = a[0];
        double Need(int n) => a.Count == n ? x : throw new CalcException($"{name} needs {n} value(s)");
        switch (name)
        {
            case "sin": Need(1); return Snap(Math.Sin(ToRad(x)));
            case "cos": Need(1); return Snap(Math.Cos(ToRad(x)));
            case "tan":
            {
                Need(1);
                if (Degrees && Math.Abs(Math.IEEERemainder(x - 90, 180)) < 1e-12) throw new CalcException("Undefined");
                return Snap(Math.Tan(ToRad(x)));
            }
            case "sec": Need(1); return 1 / NonZero(Snap(Math.Cos(ToRad(x))));
            case "csc": Need(1); return 1 / NonZero(Snap(Math.Sin(ToRad(x))));
            case "cot": Need(1); return 1 / NonZero(Snap(Math.Tan(ToRad(x))));
            case "asin": Need(1); return FromRad(Domain(Math.Asin(x)));
            case "acos": Need(1); return FromRad(Domain(Math.Acos(x)));
            case "atan": Need(1); return FromRad(Math.Atan(x));
            case "sinh": Need(1); return Math.Sinh(x);
            case "cosh": Need(1); return Math.Cosh(x);
            case "tanh": Need(1); return Math.Tanh(x);
            case "asinh": Need(1); return Math.Asinh(x);
            case "acosh": Need(1); return Domain(Math.Acosh(x));
            case "atanh": Need(1); return Domain(Math.Atanh(x));
            case "ln": Need(1); return Domain(Log(x, Math.E));
            case "log": Need(1); return Domain(Log(x, 10));
            case "lbin": Need(1); return Domain(Log(x, 2));
            case "logb": Need(2); return Domain(Log(a[1], a[0]));
            case "sqrt": Need(1); if (x < 0) throw new CalcException("Not a real number"); return Math.Sqrt(x);
            case "root": Need(2); return NthRoot(a[1], a[0]);
            case "abs": Need(1); return Math.Abs(x);
            case "exp": Need(1); return Math.Exp(x);
            case "floor": Need(1); return Math.Floor(x);
            case "ceil": Need(1); return Math.Ceiling(x);
            case "round": Need(1); return Math.Round(x, MidpointRounding.AwayFromZero);
            case "inv": Need(1); return 1 / NonZero(x);
            default: throw new CalcException($"Unknown '{name}'");
        }
    }

    private static double NonZero(double x) => x == 0 ? throw new CalcException("Undefined") : x;
    private static double Domain(double x) => double.IsNaN(x) ? throw new CalcException("Not a real number") : x;

    private static double Log(double x, double b)
    {
        if (x <= 0 || b <= 0 || b == 1) throw new CalcException("Not a real number");
        if (b == 10) return Math.Log10(x);
        if (b == 2) return Math.Log2(x);
        return Math.Log(x) / Math.Log(b);
    }

    private static double NthRoot(double x, double n)
    {
        if (n == 0) throw new CalcException("0th root is undefined");
        if (x >= 0)
        {
            double r = Math.Pow(x, 1 / n);
            double rr = Math.Round(r);
            return Math.Abs(Math.Pow(rr, n) - x) < 1e-9 * Math.Max(1, Math.Abs(x)) ? rr : r;
        }
        // Odd integer roots of negative numbers are real: ∛(−8) = −2.
        if (Math.Abs(n % 2) == 1) return -NthRoot(-x, n);
        throw new CalcException("Not a real number");
    }

    private static double Pow(double b, double e)
    {
        if (b < 0 && Math.Abs(e) < 1 && e != 0 && !IsInteger(e))
        {
            // e = 1/odd (e.g. (−8)^(1/3)): use the real root.
            double inv = 1 / e;
            if (IsInteger(inv) && Math.Abs(Math.Round(inv) % 2) == 1) return NthRoot(b, Math.Round(inv));
        }
        if (b == 0 && e < 0) throw new CalcException("Can't divide by 0");
        var r = Math.Pow(b, e);
        if (double.IsNaN(r)) throw new CalcException("Not a real number");
        return r;
    }

    private static bool IsInteger(double x) => Math.Abs(x - Math.Round(x)) < 1e-12;

    public static double Factorial(double x)
    {
        if (IsInteger(x))
        {
            long n = (long)Math.Round(x);
            if (n < 0) throw new CalcException("Undefined");
            if (n > 170) return double.PositiveInfinity;
            double r = 1;
            for (long i = 2; i <= n; i++) r *= i;
            return r;
        }
        return Gamma(x + 1);
    }

    // Lanczos approximation.
    private static double Gamma(double z)
    {
        if (z < 0.5) return Math.PI / (Math.Sin(Math.PI * z) * Gamma(1 - z));
        z -= 1;
        double[] g =
        {
            0.99999999999980993, 676.5203681218851, -1259.1392167224028, 771.32342877765313,
            -176.61502916214059, 12.507343278686905, -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7,
        };
        double x = g[0];
        for (int i = 1; i < 9; i++) x += g[i] / (z + i);
        double t = z + 7.5;
        return Math.Sqrt(2 * Math.PI) * Math.Pow(t, z + 0.5) * Math.Exp(-t) * x;
    }

    private static double Combinations(double n, double k)
    {
        if (!IsInteger(n) || !IsInteger(k) || k < 0 || n < 0 || k > n) throw new CalcException("Needs whole numbers, r ≤ n");
        k = Math.Min(k, n - k);
        double r = 1;
        for (int i = 1; i <= k; i++) r = r * (n - k + i) / i;
        return Math.Round(r);
    }

    private static double Permutations(double n, double k)
    {
        if (!IsInteger(n) || !IsInteger(k) || k < 0 || n < 0 || k > n) throw new CalcException("Needs whole numbers, r ≤ n");
        double r = 1;
        for (int i = 0; i < k; i++) r *= n - i;
        return r;
    }
}
