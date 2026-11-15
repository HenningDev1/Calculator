namespace Calculator.MathCore;

/// <summary>Structured math input with a cursor that can move into fractions, roots and exponents.</summary>
public sealed class Editor
{
    public MathRow Root { get; private set; } = new();
    public MathRow Row { get; private set; }
    public int Index { get; private set; }

    public Editor() { Row = Root; }

    public bool IsEmpty => Root.IsEmpty;

    public void Clear() { Root = new MathRow(); Row = Root; Index = 0; }

    public void Load(MathRow row)
    {
        Root = row.CloneDeep();
        Row = Root;
        Index = Root.Items.Count;
    }

    public string ToEval() => Root.ToEval();

    public void SetCursor(MathRow row, int index) { Row = row; Index = Math.Clamp(index, 0, row.Items.Count); }

    // ---------- inserting ----------

    public void Insert(Glyph g)
    {
        Row.Insert(Index, g);
        Index++;
    }

    /// <summary>Inserts a structure and puts the cursor in its first empty slot.</summary>
    public void InsertStructure(Node n, int focusSlot = 0)
    {
        Row.Insert(Index, n);
        Index++;
        var slot = n.Slots[Math.Min(focusSlot, n.Slots.Count - 1)];
        Row = slot; Index = slot.Items.Count;
    }

    /// <summary>
    /// Fraction: if a number (or bracketed group) stands just left of the cursor, it becomes the
    /// numerator and the cursor jumps to the denominator, like typing "3 ÷" on paper.
    /// </summary>
    public void InsertFraction()
    {
        var f = new Fraction();
        int start = OperandStart(Row, Index);
        if (start < Index)
        {
            var moved = Row.Items.GetRange(start, Index - start);
            Row.Items.RemoveRange(start, Index - start);
            foreach (var m in moved) f.Num.Insert(f.Num.Items.Count, m);
            Index = start;
            Row.Insert(Index, f); Index++;
            Row = f.Den; Index = 0;
        }
        else InsertStructure(f);
    }

    /// <summary>Wraps the operand left of the cursor in a structure slot (used for |x| etc.).</summary>
    public void WrapOperand(Node n, MathRow target)
    {
        int start = OperandStart(Row, Index);
        if (start == Index) { InsertStructure(n); return; }
        var moved = Row.Items.GetRange(start, Index - start);
        Row.Items.RemoveRange(start, Index - start);
        foreach (var m in moved) target.Insert(target.Items.Count, m);
        Index = start;
        Row.Insert(Index, n); Index++;
    }

    public bool HasOperandBefore => OperandStart(Row, Index) < Index;

    private static int OperandStart(MathRow row, int index)
    {
        int i = index;
        if (i == 0) return 0;
        var last = row.Items[i - 1];
        if (last is Glyph { Text: ")" })
        {
            int depth = 0;
            for (int j = i - 1; j >= 0; j--)
            {
                if (row.Items[j] is Glyph g)
                {
                    if (g.Text == ")") depth++;
                    else if (g.Text.EndsWith('(')) { depth--; if (depth == 0) return j; }
                }
            }
            return 0;
        }
        if (last is Glyph { IsNumberPart: true })
        {
            while (i > 0 && row.Items[i - 1] is Glyph { IsNumberPart: true }) i--;
            return i;
        }
        if (last is Glyph { Kind: GlyphKind.Symbol } s && s.Text != "(" && s.Text != ",") return i - 1; // π, e, Ans
        if (last is Fraction or MathCore.Root or Abs or LogBase) return i - 1;
        return index;
    }

    // ---------- deleting ----------

    public void Backspace()
    {
        if (Index > 0)
        {
            var n = Row.Items[Index - 1];
            if (n.Slots.Count == 0 || n.Slots.All(s => s.IsEmpty))
            {
                Row.Items.RemoveAt(Index - 1);
                Index--;
            }
            else
            {
                // Step inside the structure instead of deleting all of it at once.
                var last = n.Slots[^1];
                Row = last; Index = last.Items.Count;
            }
            return;
        }

        if (Row.Owner is { } owner && owner.Parent is { } parent)
        {
            int ownerIdx = parent.Items.IndexOf(owner);
            if (owner.Slots.All(s => s.IsEmpty))
            {
                parent.Items.RemoveAt(ownerIdx);
                Row = parent; Index = ownerIdx;
                return;
            }
            // Backspace at the start of a slot: move to the previous slot, or unwrap the structure.
            int slot = IndexOfSlot(owner, Row);
            if (slot > 0) { Row = owner.Slots[slot - 1]; Index = Row.Items.Count; return; }
            // Unwrap: replace the structure with the contents of its slots.
            parent.Items.RemoveAt(ownerIdx);
            int at = ownerIdx;
            foreach (var s in owner.Slots)
                foreach (var item in s.Items.ToList()) { parent.Insert(at++, item); }
            Row = parent; Index = ownerIdx;
        }
    }

    public void Delete()
    {
        if (Index < Row.Items.Count) { MoveRight(); Backspace(); }
    }

    // ---------- moving ----------

    public void MoveLeft()
    {
        if (Index > 0)
        {
            var n = Row.Items[Index - 1];
            if (n.Slots.Count > 0) { Row = n.Slots[^1]; Index = Row.Items.Count; }
            else Index--;
            return;
        }
        if (Row.Owner is { } owner && owner.Parent is { } parent)
        {
            int slot = IndexOfSlot(owner, Row);
            if (slot > 0) { Row = owner.Slots[slot - 1]; Index = Row.Items.Count; }
            else { Row = parent; Index = parent.Items.IndexOf(owner); }
        }
    }

    public void MoveRight()
    {
        if (Index < Row.Items.Count)
        {
            var n = Row.Items[Index];
            if (n.Slots.Count > 0) { Row = n.Slots[0]; Index = 0; }
            else Index++;
            return;
        }
        ExitRight();
    }

    /// <summary>Leaves the current slot to the right (moves to the next slot, or out of the structure).</summary>
    public bool ExitRight()
    {
        if (Row.Owner is { } owner && owner.Parent is { } parent)
        {
            int slot = IndexOfSlot(owner, Row);
            if (slot < owner.Slots.Count - 1) { Row = owner.Slots[slot + 1]; Index = 0; }
            else { Row = parent; Index = parent.Items.IndexOf(owner) + 1; }
            return true;
        }
        return false;
    }

    public void MoveUp()
    {
        for (var row = Row; row.Owner is { } owner; row = owner.Parent!)
        {
            switch (owner)
            {
                case Fraction f when row == f.Den: Row = f.Num; Index = Math.Min(Index, Row.Items.Count); return;
                case MathCore.Root r when !r.Square && row == r.Radicand: Row = r.Index; Index = Row.Items.Count; return;
            }
            if (owner.Parent == null) break;
        }
        // Not inside anything with an "above": if a power is right of the cursor, step into it.
        if (Index < Row.Items.Count && Row.Items[Index] is Power p) { Row = p.Exp; Index = 0; }
    }

    public void MoveDown()
    {
        for (var row = Row; row.Owner is { } owner; row = owner.Parent!)
        {
            switch (owner)
            {
                case Fraction f when row == f.Num: Row = f.Den; Index = Math.Min(Index, Row.Items.Count); return;
                case MathCore.Root r when !r.Square && row == r.Index: Row = r.Radicand; Index = 0; return;
                case Power p when row == p.Exp: Row = p.Parent!; Index = Row.Items.IndexOf(p) + 1; return;
                case LogBase l when row == l.Base: Row = l.Arg; Index = 0; return;
            }
            if (owner.Parent == null) break;
        }
    }

    public void Home() { Row = Root; Index = 0; }
    public void End() { Row = Root; Index = Root.Items.Count; }

    private static int IndexOfSlot(Node owner, MathRow row)
    {
        var slots = owner.Slots;
        for (int i = 0; i < slots.Count; i++) if (slots[i] == row) return i;
        return 0;
    }
}

