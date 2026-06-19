using Connect4HoopsArcade.Web.Models;

namespace Connect4HoopsArcade.Web.Services.Abstractions;

public interface ISettingsStore
{
    GameSettings Current { get; }
    event Action? Changed;
    Task LoadAsync();
    Task SaveAsync();
    Task ApplyAsync();   // push current settings into GameSession + AudioService
}
