namespace Connect4HoopsArcade.Core.Narration;

/// <summary>
/// Pure helper: pick an index in [0,count) that differs from the previous pick, given a caller-supplied
/// random roll. Deterministic for a fixed roll so it can be unit-tested.
/// </summary>
public static class VoicePicker
{
    public static int Pick(int count, int lastIndex, int roll)
    {
        if (count <= 1) return 0;
        if (lastIndex < 0 || lastIndex >= count) return Mod(roll, count);
        int r = Mod(roll, count - 1);          // choose among the count-1 candidates that aren't lastIndex
        return r >= lastIndex ? r + 1 : r;      // skip over lastIndex
    }

    private static int Mod(int a, int m) => ((a % m) + m) % m;   // non-negative even for negative roll
}
