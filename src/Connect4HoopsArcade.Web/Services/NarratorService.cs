using Connect4HoopsArcade.Web.Services.Abstractions;
using Connect4HoopsArcade.Web.State;

namespace Connect4HoopsArcade.Web.Services;

/// <summary>
/// Maps game events to SFX + (sparingly) voice lines. Voice is reserved for key moments; ordinary moves
/// use SFX only. Cooldowns prevent physical double-triggers from spamming sound.
/// </summary>
public sealed class NarratorService : IDisposable
{
    private readonly GameSession _session;
    private readonly IAudioService _audio;
    private static readonly Random Rng = new();

    public NarratorService(GameSession session, IAudioService audio)
    {
        _session = session;
        _audio = audio;
        _session.GameStarted += OnGameStarted;
        _session.ChipDropped += OnChipDropped;
        _session.TurnChanged += OnTurnChanged;
        _session.ColumnFull  += OnColumnFull;
        _session.ThreatRaised += OnThreat;
        _session.Won += OnWon;
        _session.Drew += OnDrew;
    }

    private async void OnGameStarted() => await _audio.PlayVoiceAsync(AudioKeys.GetReady);

    private async void OnChipDropped()
    {
        await _audio.PlaySfxAsync(AudioKeys.ChipDrop);
        // Occasional praise (~1 in 8). The voice queue serializes it after any current line.
        if (Rng.Next(8) == 0) await _audio.PlayRandomVoiceAsync(AudioKeys.GreatMove);
    }

    private async void OnTurnChanged(int current)
    {
        await _audio.PlaySfxAsync(AudioKeys.TurnChange, cooldownMs: 300);
        // Announce the turn only sometimes (~40%) so it doesn't narrate every single move.
        if (Rng.Next(10) < 4)
            await _audio.PlayRandomVoiceAsync(current == 0 ? AudioKeys.PlayerOneTurn : AudioKeys.PlayerTwoTurn);
    }

    private async void OnColumnFull()
    {
        await _audio.PlaySfxAsync(AudioKeys.ColumnFull, cooldownMs: 800);
        await _audio.PlayRandomVoiceAsync(AudioKeys.ColumnFullV);
    }

    private async void OnThreat()
    {
        await _audio.PlaySfxAsync(AudioKeys.AlmostWin, cooldownMs: 800);
        await _audio.PlayRandomVoiceAsync(AudioKeys.AlmostWinV);
    }

    private async void OnWon(int winner)
    {
        // Stinger now; interrupt any in-flight turn line so the fanfare is clean.
        await _audio.PlaySfxAsync(AudioKeys.VictorySfx);
        await _audio.PlayVoiceAsync(AudioKeys.ConnectFourV, interrupt: true);
        await _audio.PlayRandomVoiceAsync(AudioKeys.VictoryV);     // queued after connect-four line
        // Celebratory "win" cheer, offset so it follows the stinger instead of overlapping it.
        await Task.Delay(600);
        await _audio.PlaySfxAsync(AudioKeys.WinSfx);
    }

    private async void OnDrew()
    {
        await _audio.PlaySfxAsync(AudioKeys.DrawSfx);
        await _audio.PlayRandomVoiceAsync(AudioKeys.DrawV, interrupt: true);
    }

    public void Dispose()
    {
        _session.GameStarted -= OnGameStarted;
        _session.ChipDropped -= OnChipDropped;
        _session.TurnChanged -= OnTurnChanged;
        _session.ColumnFull  -= OnColumnFull;
        _session.ThreatRaised -= OnThreat;
        _session.Won -= OnWon;
        _session.Drew -= OnDrew;
    }
}
