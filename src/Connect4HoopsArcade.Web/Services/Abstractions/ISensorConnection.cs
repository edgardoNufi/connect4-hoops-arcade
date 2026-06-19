using Connect4HoopsArcade.Web.Input;

namespace Connect4HoopsArcade.Web.Services.Abstractions;

/// <summary>Physical-board channel: connection state + column events. Simulated until ESP32 is wired.</summary>
public interface ISensorConnection : IMoveSource
{
    bool Connected { get; }
    event Action<bool>? ConnectionChanged;
    void Pulse(int col);              // simulate a sensor firing (sensor-test / keyboard sim)
    void SetConnected(bool connected);
}
