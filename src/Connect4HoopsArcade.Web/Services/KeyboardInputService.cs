using Microsoft.JSInterop;
using Connect4HoopsArcade.Web.Input;

namespace Connect4HoopsArcade.Web.Services;

/// <summary>Bridges window keydown (1-7) from arcade.js into the MoveRouter as a Keyboard origin.</summary>
public sealed class KeyboardInputService : IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private readonly MoveRouter _router;
    private readonly ISensorConnectionProxy _sensor;
    private DotNetObjectReference<KeyboardInputService>? _ref;

    public KeyboardInputService(IJSRuntime js, MoveRouter router, ISensorConnectionProxy sensor)
    {
        _js = js; _router = router; _sensor = sensor;
    }

    public async Task RegisterAsync()
    {
        _ref = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("ArcadeKeyboard.register", _ref);
    }

    [JSInvokable]
    public async Task OnColumnKey(int col)
    {
        // Keyboard doubles as a sensor simulator (lights the sensor-test panel) and a move source.
        _sensor.Pulse(col);
        await _router.Route(col, MoveOrigin.Keyboard);
    }

    public ValueTask DisposeAsync()
    {
        _ref?.Dispose();
        return ValueTask.CompletedTask;
    }
}

/// <summary>Thin proxy so the keyboard service can flash the sensor panel without a hard dependency cycle.</summary>
public interface ISensorConnectionProxy { void Pulse(int col); }
