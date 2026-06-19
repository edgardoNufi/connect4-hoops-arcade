using Connect4HoopsArcade.Web.Services.Abstractions;

namespace Connect4HoopsArcade.Web.Services;

/// <summary>
/// In-memory sensor channel. A future WebSocket/ESP32 client replaces the internals and raises the same
/// events — game logic is untouched. Default disconnected (no hardware in this phase).
/// </summary>
public sealed class SensorConnectionService : ISensorConnection, ISensorConnectionProxy
{
    public bool Connected { get; private set; }
    public event Action<int>? ColumnTriggered;
    public event Action<bool>? ConnectionChanged;

    public void Pulse(int col) => ColumnTriggered?.Invoke(col);

    public void SetConnected(bool connected)
    {
        if (Connected == connected) return;
        Connected = connected;
        ConnectionChanged?.Invoke(connected);
    }
}
