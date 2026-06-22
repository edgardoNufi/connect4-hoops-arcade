namespace Connect4HoopsArcade.Core.Practice;

/// <summary>Undo/redo bookkeeping for practice mode. "Undo a turn" pops the trailing CPU move (odd index)
/// plus the human move before it; a new Play() clears the redo stack. Pure + deterministic (stores the
/// exact columns, so a redone CPU move is reproduced rather than recomputed).</summary>
public sealed class MoveLog
{
    private readonly List<int> _played = new();
    private readonly List<int> _undone = new();   // in play order; redo consumes from the front

    public IReadOnlyList<int> Played => _played;
    public bool CanUndo => _played.Count > 0;
    public bool CanRedo => _undone.Count > 0;

    public void Play(int col) { _played.Add(col); _undone.Clear(); }

    public void UndoTurn()
    {
        if (_played.Count == 0) return;
        int lastIdx = _played.Count - 1;
        bool lastWasCpu = lastIdx % 2 == 1;
        var popped = new List<int> { _played[lastIdx] };
        _played.RemoveAt(lastIdx);
        if (lastWasCpu && _played.Count > 0)
        {
            popped.Add(_played[^1]);
            _played.RemoveAt(_played.Count - 1);
        }
        popped.Reverse();                 // back to play order: [human, (cpu)]
        _undone.InsertRange(0, popped);
    }

    public void Redo()
    {
        if (_undone.Count == 0) return;
        _played.Add(_undone[0]); _undone.RemoveAt(0);          // human move
        if (_undone.Count > 0) { _played.Add(_undone[0]); _undone.RemoveAt(0); } // its CPU reply
    }

    public void Clear() { _played.Clear(); _undone.Clear(); }
}
