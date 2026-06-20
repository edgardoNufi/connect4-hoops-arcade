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
        // Keep the persisted mode in sync with runtime changes from ANY source (manual toggle OR
        // sensor autodetect), so Current.Mode is the single source of truth and a later ApplyAsync
        // (e.g. from a volume change) can't revert an autodetected mode. Singleton lifetime ⇒ no unsubscribe.
        _session.ModeChanged += OnModeChanged;
    }

    private void OnModeChanged()
    {
        if (Current.Mode == _session.Mode2) return;   // already in sync (e.g. ApplyAsync drove it)
        Current.Mode = _session.Mode2;
        _ = PersistAsync();                            // persist only; do NOT re-Apply (avoids a loop)
        Changed?.Invoke();
    }

    private async Task PersistAsync()
    {
        try { await _js.InvokeVoidAsync("ArcadeStore.set", Key, JsonSerializer.Serialize(Current)); }
        catch { /* storage may be unavailable; ignore */ }
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
        await PersistAsync();
        await ApplyAsync();
        Changed?.Invoke();
    }

    public async Task ApplyAsync()
    {
        _session.Speed = Current.Speed;
        _session.NarratorTone = Current.Tone;
        _session.CpuLevel = Current.CpuLevel;
        _session.SetPlayMode(Current.Mode);
        _audio.VoicesEnabled = Current.VoicesEnabled;
        await _audio.SetVolumesAsync(Current.SfxVolume, Current.NarratorVolume, Current.MusicVolume);
    }
}
