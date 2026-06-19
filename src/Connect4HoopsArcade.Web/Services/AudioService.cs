using Microsoft.JSInterop;
using Connect4HoopsArcade.Web.Services.Abstractions;

namespace Connect4HoopsArcade.Web.Services;

public sealed class AudioService : IAudioService
{
    private readonly IJSRuntime _js;
    private bool _initialized;
    private static readonly Random Rng = new();

    public bool VoicesEnabled { get; set; } = true;

    public AudioService(IJSRuntime js) => _js = js;

    public async Task InitAsync()
    {
        if (_initialized) return;
        _initialized = true;
        await Safe("ArcadeAudio.init");
    }

    public Task PlaySfxAsync(string key, int cooldownMs = 0) => Safe("ArcadeAudio.playSfx", key, cooldownMs);

    public Task PlayVoiceAsync(string key, bool interrupt = false) =>
        VoicesEnabled ? Safe("ArcadeAudio.playVoice", key, interrupt) : Task.CompletedTask;

    public Task PlayRandomVoiceAsync(IReadOnlyList<string> keys, bool interrupt = false)
    {
        if (!VoicesEnabled || keys.Count == 0) return Task.CompletedTask;
        return PlayVoiceAsync(keys[Rng.Next(keys.Count)], interrupt);
    }

    public Task PlayMusicAsync(string key, bool loop = true) => Safe("ArcadeAudio.playMusic", key, loop);
    public Task StopMusicAsync() => Safe("ArcadeAudio.stopMusic");
    public Task SetVolumesAsync(int sfx, int voice, int music) =>
        Safe("ArcadeAudio.setVolumes", sfx / 100.0, voice / 100.0, music / 100.0);
    public Task MuteAsync() => Safe("ArcadeAudio.mute");
    public Task UnmuteAsync() => Safe("ArcadeAudio.unmute");

    private async Task Safe(string fn, params object[] args)
    {
        try { await _js.InvokeVoidAsync(fn, args); }
        catch (Exception e) { Console.WriteLine($"[Audio] {fn} failed: {e.Message}"); }
    }
}
