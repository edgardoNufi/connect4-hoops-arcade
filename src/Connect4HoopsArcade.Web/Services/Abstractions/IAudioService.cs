namespace Connect4HoopsArcade.Web.Services.Abstractions;

public interface IAudioService
{
    Task InitAsync();
    Task PlaySfxAsync(string key, int cooldownMs = 0);
    Task PlayVoiceAsync(string key, bool interrupt = false);
    Task PlayRandomVoiceAsync(IReadOnlyList<string> keys, bool interrupt = false);
    Task PlayMusicAsync(string key, bool loop = true);
    Task StopMusicAsync();
    Task SetVolumesAsync(int sfx, int voice, int music);
    Task MuteAsync();
    Task UnmuteAsync();
    bool VoicesEnabled { get; set; }
}
