using System.Text.Json;
using Microsoft.JSInterop;
using Connect4HoopsArcade.Web.Models;
using Connect4HoopsArcade.Web.Services.Abstractions;
using Connect4HoopsArcade.Web.State;

namespace Connect4HoopsArcade.Web.Services;

public sealed class SettingsStore : ISettingsStore
{
    private const string Key = "c4h.settings";
    private readonly IJSRuntime _js;
    private readonly GameSession _session;
    private readonly IAudioService _audio;

    public GameSettings Current { get; private set; } = new();
    public event Action? Changed;

    public SettingsStore(IJSRuntime js, GameSession session, IAudioService audio)
    {
        _js = js; _session = session; _audio = audio;
    }

    public async Task LoadAsync()
    {
        try
        {
            var json = await _js.InvokeAsync<string?>("ArcadeStore.get", Key);
            if (!string.IsNullOrWhiteSpace(json))
                Current = JsonSerializer.Deserialize<GameSettings>(json) ?? new();
        }
        catch { Current = new(); }
        await ApplyAsync();
        Changed?.Invoke();
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(Current);
            await _js.InvokeVoidAsync("ArcadeStore.set", Key, json);
        }
        catch { /* storage may be unavailable; ignore */ }
        await ApplyAsync();
        Changed?.Invoke();
    }

    public async Task ApplyAsync()
    {
        _session.Speed = Current.Speed;
        _session.SetPlayMode(Current.Mode);
        _audio.VoicesEnabled = Current.VoicesEnabled;
        await _audio.SetVolumesAsync(Current.SfxVolume, Current.NarratorVolume, Current.MusicVolume);
    }
}
