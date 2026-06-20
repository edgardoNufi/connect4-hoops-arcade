using Microsoft.JSInterop;
using Connect4HoopsArcade.Web.Services.Abstractions;

namespace Connect4HoopsArcade.Web.Services;

/// <summary>Wraps window.ArcadeFullscreen. Mirrors ViewportService: a DotNetObjectReference receives
/// fullscreenchange callbacks so the button icon stays in sync even on Esc/system-gesture exit.</summary>
public sealed class FullscreenService : IFullscreenService, IDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<FullscreenService>? _ref;

    public bool IsActive { get; private set; }
    public bool IsApiSupported { get; private set; }
    public bool IsIOSPhone { get; private set; }
    public event Action? StateChanged;

    public FullscreenService(IJSRuntime js) => _js = js;

    public async Task InitAsync()
    {
        IsApiSupported = await _js.InvokeAsync<bool>("ArcadeFullscreen.isApiSupported");
        IsIOSPhone = await _js.InvokeAsync<bool>("ArcadeFullscreen.isIOSPhone");
        IsActive = await _js.InvokeAsync<bool>("ArcadeFullscreen.isActive");
        _ref = DotNetObjectReference.Create(this);
        await _js.InvokeVoidAsync("ArcadeFullscreen.register", _ref);
    }

    public async Task ToggleAsync()
    {
        IsActive = await _js.InvokeAsync<bool>("ArcadeFullscreen.toggle");
        StateChanged?.Invoke();
    }

    [JSInvokable]
    public void OnFullscreenChanged(bool active)
    {
        IsActive = active;
        StateChanged?.Invoke();
    }

    public void Dispose() => _ref?.Dispose();
}
