using Avalonia.Input;

namespace Calculator;

public enum KeyStyle { Number, Function, Utility, Operator }

/// <summary>Everything the calculator can do. Every entry can be bound to a hotkey.</summary>
public sealed record Command(string Id, string Name, string Label, KeyStyle Style, string DefaultKey = "", string Group = "Basic");

public static class Commands
{
    public static readonly List<Command> All = new()
    {
        // Digits & basics
        new("d0", "0", "0", KeyStyle.Number, "", "Digits"),
        new("d1", "1", "1", KeyStyle.Number, "", "Digits"),
        new("d2", "2", "2", KeyStyle.Number, "", "Digits"),
        new("d3", "3", "3", KeyStyle.Number, "", "Digits"),
        new("d4", "4", "4", KeyStyle.Number, "", "Digits"),
        new("d5", "5", "5", KeyStyle.Number, "", "Digits"),
        new("d6", "6", "6", KeyStyle.Number, "", "Digits"),
        new("d7", "7", "7", KeyStyle.Number, "", "Digits"),
        new("d8", "8", "8", KeyStyle.Number, "", "Digits"),
        new("d9", "9", "9", KeyStyle.Number, "", "Digits"),
        new("dot", "Decimal point", ".", KeyStyle.Number, "", "Digits"),

        new("add", "Add", "+", KeyStyle.Operator),
        new("sub", "Subtract", "−", KeyStyle.Operator),
        new("mul", "Multiply", "×", KeyStyle.Operator),
        new("div", "Divide", "÷", KeyStyle.Operator),
        new("eq", "Equals", "=", KeyStyle.Operator, "Enter"),
        new("neg", "Change sign", "±", KeyStyle.Number, "F9"),
        new("percent", "Percent", "%", KeyStyle.Utility),
        new("clear", "Clear all", "AC", KeyStyle.Utility, "Escape"),
        new("back", "Backspace", "⌫", KeyStyle.Utility, "Back"),
        new("del", "Delete right", "Del", KeyStyle.Utility, "Delete"),
        new("lparen", "Open bracket", "(", KeyStyle.Function),
        new("rparen", "Close bracket", ")", KeyStyle.Function),

        // Powers, roots, fractions
        new("frac", "Fraction", "a/b", KeyStyle.Function, "F", "Powers & roots"),
        new("sqrt", "Square root", "√x", KeyStyle.Function, "R", "Powers & roots"),
        new("cbrt", "Cube root", "∛x", KeyStyle.Function, "Shift+R", "Powers & roots"),
        new("nroot", "n-th root", "ⁿ√x", KeyStyle.Function, "Ctrl+R", "Powers & roots"),
        new("pow", "Power xʸ", "xʸ", KeyStyle.Function, "Y", "Powers & roots"),
        new("square", "Square x²", "x²", KeyStyle.Function, "Q", "Powers & roots"),
        new("cube", "Cube x³", "x³", KeyStyle.Function, "Shift+Q", "Powers & roots"),
        new("inv", "Reciprocal 1/x", "1/x", KeyStyle.Function, "I", "Powers & roots"),
        new("exp", "e to the x", "eˣ", KeyStyle.Function, "Shift+E", "Powers & roots"),
        new("pow10", "10 to the x", "10ˣ", KeyStyle.Function, "Shift+G", "Powers & roots"),
        new("pow2", "2 to the x", "2ˣ", KeyStyle.Function, "", "Powers & roots"),
        new("ee", "Scientific notation (×10ⁿ)", "EE", KeyStyle.Function, "Ctrl+E", "Powers & roots"),
        new("abs", "Absolute value", "|x|", KeyStyle.Function, "B", "Powers & roots"),
        new("fact", "Factorial", "x!", KeyStyle.Function, "", "Powers & roots"),

        // Logs
        new("ln", "Natural log", "ln", KeyStyle.Function, "N", "Logarithms"),
        new("log", "Log base 10", "log", KeyStyle.Function, "G", "Logarithms"),
        new("log2", "Log base 2", "log₂", KeyStyle.Function, "Shift+L", "Logarithms"),
        new("logb", "Log with any base", "logₙ", KeyStyle.Function, "L", "Logarithms"),

        // Trig
        new("sin", "Sine", "sin", KeyStyle.Function, "S", "Trigonometry"),
        new("cos", "Cosine", "cos", KeyStyle.Function, "O", "Trigonometry"),
        new("tan", "Tangent", "tan", KeyStyle.Function, "T", "Trigonometry"),
        new("asin", "Inverse sine", "sin⁻¹", KeyStyle.Function, "Shift+S", "Trigonometry"),
        new("acos", "Inverse cosine", "cos⁻¹", KeyStyle.Function, "Shift+O", "Trigonometry"),
        new("atan", "Inverse tangent", "tan⁻¹", KeyStyle.Function, "Shift+T", "Trigonometry"),
        new("sinh", "Hyperbolic sine", "sinh", KeyStyle.Function, "Ctrl+S", "Trigonometry"),
        new("cosh", "Hyperbolic cosine", "cosh", KeyStyle.Function, "Ctrl+O", "Trigonometry"),
        new("tanh", "Hyperbolic tangent", "tanh", KeyStyle.Function, "Ctrl+T", "Trigonometry"),
        new("asinh", "Inverse hyperbolic sine", "sinh⁻¹", KeyStyle.Function, "Ctrl+Shift+S", "Trigonometry"),
        new("acosh", "Inverse hyperbolic cosine", "cosh⁻¹", KeyStyle.Function, "Ctrl+Shift+O", "Trigonometry"),
        new("atanh", "Inverse hyperbolic tangent", "tanh⁻¹", KeyStyle.Function, "Ctrl+Shift+T", "Trigonometry"),
        new("sec", "Secant", "sec", KeyStyle.Function, "", "Trigonometry"),
        new("csc", "Cosecant", "csc", KeyStyle.Function, "", "Trigonometry"),
        new("cot", "Cotangent", "cot", KeyStyle.Function, "", "Trigonometry"),
        new("angle", "Toggle degrees / radians", "Deg", KeyStyle.Function, "Ctrl+D", "Trigonometry"),

        // Constants & misc
        new("pi", "Pi", "π", KeyStyle.Function, "P", "More"),
        new("e", "Euler's number", "e", KeyStyle.Function, "E", "More"),
        new("ans", "Last answer", "Ans", KeyStyle.Function, "A", "More"),
        new("rand", "Random number", "Rand", KeyStyle.Function, "", "More"),
        new("mod", "Modulo", "mod", KeyStyle.Function, "M", "More"),
        new("ncr", "Combinations nCr", "nCr", KeyStyle.Function, "Shift+C", "More"),
        new("npr", "Permutations nPr", "nPr", KeyStyle.Function, "Shift+P", "More"),
        new("floor", "Round down", "floor", KeyStyle.Function, "", "More"),
        new("ceil", "Round up", "ceil", KeyStyle.Function, "", "More"),
        new("round", "Round", "round", KeyStyle.Function, "", "More"),
        new("sd", "Fraction ⇄ decimal result", "S↔D", KeyStyle.Function, "Shift+F", "More"),
        new("mc", "Memory clear", "mc", KeyStyle.Function, "Ctrl+L", "Memory"),
        new("mplus", "Memory add", "m+", KeyStyle.Function, "Ctrl+P", "Memory"),
        new("mminus", "Memory subtract", "m−", KeyStyle.Function, "Ctrl+Q", "Memory"),
        new("mr", "Memory recall", "mr", KeyStyle.Function, "Ctrl+M", "Memory"),

        // Editing & window
        new("left", "Cursor left", "◀", KeyStyle.Function, "Left", "Editing"),
        new("right", "Cursor right", "▶", KeyStyle.Function, "Right", "Editing"),
        new("up", "Cursor up (numerator / index)", "▲", KeyStyle.Function, "Up", "Editing"),
        new("down", "Cursor down (denominator)", "▼", KeyStyle.Function, "Down", "Editing"),
        new("home", "Cursor to start", "⇤", KeyStyle.Function, "Home", "Editing"),
        new("end", "Cursor to end", "⇥", KeyStyle.Function, "End", "Editing"),
        new("copy", "Copy result", "Copy", KeyStyle.Function, "Ctrl+C", "Editing"),
        new("paste", "Paste number", "Paste", KeyStyle.Function, "Ctrl+V", "Editing"),
        new("history", "Show history", "History", KeyStyle.Function, "Ctrl+H", "Window"),
        new("topmost", "Keep window on top", "Pin", KeyStyle.Function, "Ctrl+Shift+P", "Window"),
        new("settings", "Open settings", "⚙", KeyStyle.Function, "Ctrl+OemComma", "Window"),
    };

    public static readonly Dictionary<string, Command> ById = All.ToDictionary(c => c.Id);

    public static string KeyFor(Settings s, string id) =>
        s.Hotkeys.TryGetValue(id, out var k) ? k : ById[id].DefaultKey;

    public static KeyGesture? Gesture(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try { return KeyGesture.Parse(text); } catch { return null; }
    }

    /// <summary>Friendly text for a gesture string ("Ctrl+OemComma" → "Ctrl + ,").</summary>
    public static string Pretty(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "—";
        return string.Join(" + ", text.Split('+').Select(p => p switch
        {
            "OemComma" => ",", "OemPeriod" => ".", "OemPlus" => "+", "OemMinus" => "−", "Back" => "Backspace",
            "Escape" => "Esc", "Return" => "Enter", "Next" => "PageDown", "Prior" => "PageUp",
            _ when p.Length == 2 && p[0] == 'D' && char.IsDigit(p[1]) => p[1..],
            _ => p,
        }));
    }

    // Key layouts, by window size. Each tier adds columns on the left.
    public static readonly string[][] Compact =
    {
        new[] { "sqrt", "cbrt", "nroot", "pow" },
        new[] { "square", "frac", "lparen", "rparen" },
        new[] { "back", "clear", "percent", "div" },
        new[] { "d7", "d8", "d9", "mul" },
        new[] { "d4", "d5", "d6", "sub" },
        new[] { "d1", "d2", "d3", "add" },
        new[] { "neg", "d0", "dot", "eq" },
    };

    public static readonly string[][] Medium =
    {
        new[] { "pi", "e" },
        new[] { "cube", "inv" },
        new[] { "ln", "log" },
        new[] { "exp", "pow10" },
        new[] { "fact", "abs" },
        new[] { "ee", "sd" },
        new[] { "ans", "angle" },
    };

    public static readonly string[][] Wide =
    {
        new[] { "sin", "cos", "tan", "rand" },
        new[] { "asin", "acos", "atan", "mod" },
        new[] { "sinh", "cosh", "tanh", "ncr" },
        new[] { "asinh", "acosh", "atanh", "npr" },
        new[] { "log2", "logb", "pow2", "floor" },
        new[] { "sec", "csc", "cot", "ceil" },
        new[] { "mc", "mplus", "mminus", "mr" },
    };
}
