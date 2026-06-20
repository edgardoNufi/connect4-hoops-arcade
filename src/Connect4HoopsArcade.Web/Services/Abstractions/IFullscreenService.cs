namespace Connect4HoopsArcade.Web.Services.Abstractions;

/// <summary>Fullscreen control. API works on Android/desktop/iPad; iPhone Safari falls back to PWA "Add to Home Screen".</summary>
public interface IFullscreenService
{
    bool IsActive { get; }
    bool IsApiSupported { get; }
    bool IsIOSPhone { get; }
    event Action? StateChanged;
    Task InitAsync();
    Task ToggleAsync();
}
