using Connect4HoopsArcade.Web.Models;

namespace Connect4HoopsArcade.Web.Services.Abstractions;

/// <summary>Reactive viewport info backed by JS (matchMedia + debounced resize). Isolated JS interop.</summary>
public interface IViewportService
{
    int Width { get; }
    int Height { get; }
    bool IsMobile { get; }
    bool IsTablet { get; }
    bool IsLandscape { get; }
    bool IsPortrait { get; }
    Breakpoint Breakpoint { get; }

    /// <summary>Fires only when the breakpoint or orientation changes (not on every resize pixel).</summary>
    event Action? OnViewportChanged;

    /// <summary>Registers the JS resize listener and reads the initial size. Call once at startup.</summary>
    Task InitAsync();
}
