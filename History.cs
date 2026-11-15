using System.Text.Json;
using Calculator.MathCore;

namespace Calculator;

public sealed record HistoryEntry(MathRow Expression, double Value, DateTime When);

/// <summary>Past calculations, newest first, saved to %APPDATA%\Calculator\history.json.</summary>
public sealed class History
{
    private const int Max = 200;
    public List<HistoryEntry> Entries { get; } = new();
    public event Action? Changed;

    private static string FilePath => Path.Combine(Settings.DataDir, "history.json");

    public void Add(MathRow expr, double value)
    {
        Entries.Insert(0, new HistoryEntry(expr.CloneDeep(), value, DateTime.Now));
        if (Entries.Count > Max) Entries.RemoveRange(Max, Entries.Count - Max);
        Save();
        Changed?.Invoke();
    }

    public void Clear()
    {
        Entries.Clear();
        Save();
        Changed?.Invoke();
    }

    // ---------- saving: math trees as plain records ----------

    public sealed class NodeDto
    {
        public string T { get; set; } = "g";          // g glyph, f fraction, p power, r root, a abs, l log base
        public string? Text { get; set; }
        public string? Eval { get; set; }
        public int Kind { get; set; }
        public bool Square { get; set; }
        public List<List<NodeDto>>? Slots { get; set; }
    }

    public sealed class EntryDto
    {
        public List<NodeDto> Expr { get; set; } = new();
        public double Value { get; set; }
        public DateTime When { get; set; }
    }

    private static List<NodeDto> ToDto(MathRow row) => row.Items.Select(n => n switch
    {
        Glyph g => new NodeDto { T = "g", Text = g.Text, Eval = g.Eval, Kind = (int)g.Kind },
        _ => new NodeDto
        {
            T = n switch { Fraction => "f", Power => "p", MathCore.Root => "r", Abs => "a", _ => "l" },
            Square = n is MathCore.Root { Square: true },
            Slots = n.Slots.Select(ToDto).ToList(),
        },
    }).ToList();

    private static MathRow FromDto(List<NodeDto> list)
    {
        var row = new MathRow();
        foreach (var d in list)
        {
            Node n = d.T switch
            {
                "f" => new Fraction(),
                "p" => new Power(),
                "r" => new MathCore.Root(d.Square),
                "a" => new Abs(),
                "l" => new LogBase(),
                _ => new Glyph(d.Text ?? "", d.Eval ?? "", (GlyphKind)d.Kind),
            };
            if (d.Slots != null)
                for (int i = 0; i < Math.Min(d.Slots.Count, n.Slots.Count); i++)
                {
                    var src = FromDto(d.Slots[i]);
                    foreach (var item in src.Items.ToList()) n.Slots[i].Insert(n.Slots[i].Items.Count, item);
                }
            row.Insert(row.Items.Count, n);
        }
        return row;
    }

    public static History Load()
    {
        var h = new History();
        try
        {
            if (File.Exists(FilePath))
            {
                var list = JsonSerializer.Deserialize<List<EntryDto>>(File.ReadAllText(FilePath)) ?? new();
                foreach (var e in list) h.Entries.Add(new HistoryEntry(FromDto(e.Expr), e.Value, e.When));
            }
        }
        catch { }
        return h;
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Settings.DataDir);
            var list = Entries.Select(e => new EntryDto { Expr = ToDto(e.Expression), Value = e.Value, When = e.When }).ToList();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list));
        }
        catch { }
    }
}
