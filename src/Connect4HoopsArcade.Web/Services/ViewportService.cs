using Microsoft.JSInterop;
using Connect4HoopsArcade.Web.Models;
using Connect4HoopsArcade.Web.Services.Abstractions;

namespace Connect4HoopsArcade.Web.Services;

/// <summary>Tracks viewport size/orientation via arcade.js. Notifies on breakpoint/orientation change only.</summary>
public sealed class ViewportService : IViewportService, IAsyncDisposable
{
    private readonly IJSRuntime _js;
    private DotNetObjectReference<ViewportService>? _ref;
    private bool _initialized;

    public int Width { get; private set; } = 1280;
    public int Height { get; private set; } = 800;
    public bool IsMobile { get; private set; }
    public bool IsLandscape { get; private set; } = true;
    public Breakpoint Breakpoint { get; private set; } = Breakpoint.Desktop;

    public bool IsTablet => Breakpoint == Breakpoint.Tablet && !IsMobile;
    public bool IsPortrait => !IsLandscape;

    public event Action? OnViewportChanged;

    public ViewportService(IJSRuntime js) => _js = js;

    public async Task InitAsync()
    {
        if (_initialized) return;
        _initialized = true;
        _ref = DotNetObjectReference.Create(this);
        try
        {
            var s = await _js.InvokeAsync<Snapshot>("ArcadeViewport.register", _ref);
            Apply(s.Width, s.Height, s.IsMobile, s.Breakpoint, s.IsLandscape);
        }
        catch { /* prerender / no JS: keep desktop defaults */ }
    }

    [JSInvokable]
    public void NotifyChanged(int width, int height, bool isMobile, int breakpoint, bool isLandscape)
    {
        Apply(width, height, isMobile, breakpoint, isLandscape);
        OnViewportChanged?.Invoke();
    }

    private void Apply(int width, int height, bool isMobile, int breakpoint, bool isLandscape)
    {
        Width = width; Height = height; IsMobile = isMobile; IsLandscape = isLandscape;
        Breakpoint = (Breakpoint)breakpoint;
    }

    public async ValueTask DisposeAsync()
    {
        try { await _js.InvokeVoidAsync("ArcadeViewport.dispose"); }
        catch { /* runtime tearing down */ }
        _ref?.Dispose();
    }

    private sealed record Snapshot(int Width, int Height, bool IsMobile, int Breakpoint, bool IsLandscape);
}
