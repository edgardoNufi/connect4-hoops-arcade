using Connect4HoopsArcade.Web.Models;
using Connect4HoopsArcade.Web.State;

namespace Connect4HoopsArcade.Web.Input;

/// <summary>Funnels every move source into <see cref="GameSession.TryDrop"/> with debounce + mode rules.</summary>
public sealed class MoveRouter
{
    private readonly GameSession _session;
    private int _lastCol = -1;
    private DateTime _lastAt = DateTime.MinValue;
    private static readonly TimeSpan DebounceWindow = TimeSpan.FromMilliseconds(250);

    public MoveRouter(GameSession session) => _session = session;

    public async Task Route(int col, MoveOrigin origin)
    {
        if (col < 0 || col > 6) return;
        if (!OriginAllowed(origin)) return;

        var now = DateTime.UtcNow;
        if (col == _lastCol && now - _lastAt < DebounceWindow) return;   // ignore double sensor / rapid repeat
        _lastCol = col; _lastAt = now;

        await _session.TryDrop(col);
    }

    private bool OriginAllowed(MoveOrigin origin) => _session.Mode2 switch
    {
        // Digital: screen + keyboard play; sensor events ignored (no physical board).
        PlayMode.Digital  => origin is MoveOrigin.Screen or MoveOrigin.Keyboard,
        // Physical: sensors are authoritative; keyboard simulates; on-screen clicks disabled.
        PlayMode.Physical => origin is MoveOrigin.Sensor or MoveOrigin.Keyboard,
        _ => true,
    };
}
