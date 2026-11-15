using System.Globalization;
using System.Text;

namespace Calculator.MathCore;

/// <summary>Turns a result into display rows: grouped digits, ×10ⁿ for huge/tiny values, or a stacked fraction.</summary>
public static class Formatter
{
    public static MathRow Number(double v, bool asFraction)
    {
        var row = new MathRow();
        if (double.IsPositiveInfinity(v)) { Add(row, "∞"); return row; }
        if (double.IsNegativeInfinity(v)) { Add(row, "−∞"); return row; }

        if (asFraction && TryFraction(v, out long n, out long d) && d != 1)
        {
            if (n < 0) Add(row, "−");
            var f = new Fraction();
            AddText(f.Num, Group(Math.Abs(n).ToString(CultureInfo.InvariantCulture)));
            AddText(f.Den, Group(d.ToString(CultureInfo.InvariantCulture)));
            row.Insert(row.Items.Count, f);
            return row;
        }

        double a = Math.Abs(v);
        if (a != 0 && (a >= 1e15 || a < 1e-9))
        {
            int exp = (int)Math.Floor(Math.Log10(a));
            double mant = v / Math.Pow(10, exp);
            if (Math.Abs(Math.Round(mant, 9)) >= 10) { mant /= 10; exp++; }
            AddText(row, Plain(mant, 10));
            Add(row, "×10");
            var p = new Power();
            AddText(p.Exp, exp < 0 ? "−" + (-exp) : exp.ToString(CultureInfo.InvariantCulture));
            row.Insert(row.Items.Count, p);
            return row;
        }

        AddText(row, Group(Plain(v, 12)));
        return row;
    }

    /// <summary>Plain text version (for copy to clipboard).</summary>
    public static string Text(double v)
    {
        if (double.IsInfinity(v)) return v > 0 ? "∞" : "-∞";
        double a = Math.Abs(v);
        if (a != 0 && (a >= 1e15 || a < 1e-9)) return v.ToString("0.#########E+0", CultureInfo.InvariantCulture);
        return Plain(v, 12);
    }

    private static string Plain(double v, int sig)
    {
        if (v == 0) return "0";
        int mag = (int)Math.Floor(Math.Log10(Math.Abs(v)));
        int decimals = Math.Clamp(sig - 1 - mag, 0, 15);
        var r = Math.Round(v, decimals, MidpointRounding.AwayFromZero);
        var s = r.ToString("F" + decimals, CultureInfo.InvariantCulture);
        if (s.Contains('.')) s = s.TrimEnd('0').TrimEnd('.');
        if (s == "-0") s = "0";
        return s.Replace('-', '−');
    }

    private static string Group(string s)
    {
        bool neg = s.StartsWith('−');
        if (neg) s = s[1..];
        int dot = s.IndexOf('.');
        string ip = dot < 0 ? s : s[..dot], fp = dot < 0 ? "" : s[dot..];
        var sb = new StringBuilder();
        for (int i = 0; i < ip.Length; i++)
        {
            if (i > 0 && (ip.Length - i) % 3 == 0) sb.Append(',');
            sb.Append(ip[i]);
        }
        return (neg ? "−" : "") + sb + fp;
    }

    public static bool TryFraction(double v, out long num, out long den)
    {
        num = 0; den = 1;
        if (double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 1e12) return false;
        // Continued fraction expansion.
        long h0 = 0, h1 = 1, k0 = 1, k1 = 0;
        double x = v;
        for (int i = 0; i < 40; i++)
        {
            double a = Math.Floor(x);
            long h2 = (long)a * h1 + h0, k2 = (long)a * k1 + k0;
            if (k2 > 1_000_000_000) break;
            h0 = h1; h1 = h2; k0 = k1; k1 = k2;
            if (Math.Abs(v - (double)h1 / k1) <= 1e-13 * Math.Max(1, Math.Abs(v)))
            {
                num = h1; den = k1;
                if (den < 0) { num = -num; den = -den; }
                return true;
            }
            double frac = x - a;
            if (frac < 1e-15) break;
            x = 1 / frac;
        }
        return false;
    }

    private static void Add(MathRow row, string text) => row.Insert(row.Items.Count, new Glyph(text, ""));

    private static void AddText(MathRow row, string text)
    {
        foreach (var c in text) row.Insert(row.Items.Count, new Glyph(c.ToString(), "", GlyphKind.Digit));
    }
}

