using Connect4HoopsArcade.Core.Narration;
using Connect4HoopsArcade.Core.Primitives;
using Connect4HoopsArcade.Web.Models;
using Connect4HoopsArcade.Web.Services.Abstractions;
using Connect4HoopsArcade.Web.State;

namespace Connect4HoopsArcade.Web.Services;

/// <summary>
/// Maps game events to SFX + voice. Single owner of the serial voice queue. In 1-player mode it adds
/// streak-aware taunts (see CpuTauntPolicy); in 2-player it stays neutral. NarratorTone gates/scales voice.
/// </summary>
public sealed class NarratorService : IDisposable
{
    private readonly GameSession _session;
    private readonly IAudioService _audio;
    private static readonly Random Rng = new();
    private static readonly TimeSpan MidTauntCooldown = TimeSpan.FromSeconds(25);

    private DateTime _lastThreatTaunt = DateTime.MinValue;   // threat-to-threat spacing; ALSO gates idle (priority)
    private DateTime _lastIdleNudge = DateTime.MinValue;     // generic (2P) idle-to-idle spacing
    private bool _idleTauntUsedThisRound;                    // cpu-idle is filler: max one per round
    private bool _closingVoiceActive;                        // a closing line is in flight — don't talk over it
    private readonly Dictionary<string, int> _lastIndex = new();   // last variant per category (no repeats; kept across rounds on purpose)

    public NarratorService(GameSession session, IAudioService audio)
    {
        _session = session;
        _audio = audio;
        _session.GameStarted += OnGameStarted;
        _session.RoundStarted += OnRoundStarted;
        _session.ChipDropped += OnChipDropped;
        _session.TurnChanged += OnTurnChanged;
        _session.ColumnFull  += OnColumnFull;
        _session.ThreatRaised += OnThreat;
        _session.IdleNudged  += OnIdle;
        _session.MatchEnded  += OnMatchEnded;
        _session.AudioStopRequested += OnAudioStopRequested;
    }

    // New round or leaving the game: kill any lingering audio (a 15s victory line would otherwise keep
    // playing under the next game and make new voices queue up behind it).
    private void OnAudioStopRequested() => _ = _audio.StopAllAsync();

    private bool VoiceOn => _session.NarratorTone != NarratorTone.Silencioso;
    private bool TauntsOn => _session.Mode == GameMode.OnePlayer && VoiceOn;
    private int CpuIndex =>
        _session.Players.Length > 1 && _session.Players[1].IsCpu ? 1 :
        _session.Players.Length > 0 && _session.Players[0].IsCpu ? 0 : -1;

    // Familiar caps escalation at Confident (no Boss lines); Picante uses the raw level.
    private CpuTauntLevel EffectiveLevel()
    {
        var raw = CpuTauntPolicy.LevelFor(_session.CpuWinStreak);
        return _session.NarratorTone == NarratorTone.Familiar && raw == CpuTauntLevel.BossMode
            ? CpuTauntLevel.ConfidentCpu
            : raw;
    }

    private Task Taunt(string category, IReadOnlyList<string> keys, bool interrupt = false)
    {
        if (keys.Count == 0) return Task.CompletedTask;
        int last = _lastIndex.TryGetValue(category, out var v) ? v : -1;
        int idx = VoicePicker.Pick(keys.Count, last, Rng.Next(100_000));
        _lastIndex[category] = idx;
        return _audio.PlayVoiceAsync(keys[idx], interrupt);
    }

    private bool ClosingOrOver => _session.Winner != null || _closingVoiceActive;
    private bool ThreatCooldownPassed => DateTime.UtcNow - _lastThreatTaunt >= MidTauntCooldown;

    private void OnRoundStarted()
    {
        _idleTauntUsedThisRound = false;
        _closingVoiceActive = false;
        _lastThreatTaunt = DateTime.MinValue;
        _lastIdleNudge = DateTime.MinValue;
    }

    private async void OnGameStarted()
    {
        if (VoiceOn) await _audio.PlayVoiceAsync(AudioKeys.GetReady);
    }

    private async void OnChipDropped()
    {
        await _audio.PlaySfxAsync(AudioKeys.ChipDrop);
        // Occasional praise (~1 in 8). Serialized after any current line.
        if (VoiceOn && Rng.Next(8) == 0) await _audio.PlayRandomVoiceAsync(AudioKeys.GreatMove);
    }

    private async void OnTurnChanged(int current)
    {
        await _audio.PlaySfxAsync(AudioKeys.TurnChange, cooldownMs: 300);
        if (VoiceOn && Rng.Next(10) < 4)
            await _audio.PlayRandomVoiceAsync(current == 0 ? AudioKeys.PlayerOneTurn : AudioKeys.PlayerTwoTurn);
    }

    private async void OnColumnFull()
    {
        await _audio.PlaySfxAsync(AudioKeys.ColumnFull, cooldownMs: 800);
        if (VoiceOn) await _audio.PlayRandomVoiceAsync(AudioKeys.ColumnFullV);
    }

    private async void OnThreat(int moverIndex)
    {
        // High priority: taunt whenever the CPU threatens (1P). Gated only by its own threat-to-threat
        // cooldown + match-over/closing guards — NEVER blocked by a prior idle nudge.
        if (TauntsOn && CpuIndex >= 0 && moverIndex == CpuIndex)
        {
            if (ClosingOrOver || !ThreatCooldownPassed) return;
            _lastThreatTaunt = DateTime.UtcNow;
            await Taunt("cpu-threat", CpuTauntLines.Threat(EffectiveLevel()));
        }
        else if (VoiceOn)
        {
            await _audio.PlayRandomVoiceAsync(AudioKeys.AlmostWinV);   // neutral warning (2P, or human threatening)
        }
    }

    private async void OnIdle()
    {
        if (!VoiceOn || ClosingOrOver) return;
        if (_session.Mode == GameMode.OnePlayer)
        {
            // cpu-idle is filler: at most once per round, and it yields to a RECENT threat. It deliberately
            // does NOT stamp the threat timestamp, so it can never block a threat that comes after it.
            if (_idleTauntUsedThisRound || !ThreatCooldownPassed) return;
            _idleTauntUsedThisRound = true;
            await Taunt("cpu-idle", CpuTauntLines.Idle(EffectiveLevel()));
        }
        else
        {
            // Generic nudge (2P / non-CPU); spaced by its own idle-to-idle cooldown.
            if (DateTime.UtcNow - _lastIdleNudge < MidTauntCooldown) return;
            _lastIdleNudge = DateTime.UtcNow;
            await Taunt("idle", AudioKeys.IdleNudge);
        }
    }

    private async void OnMatchEnded(int? winner, GameMode mode)
    {
        _closingVoiceActive = true;

        // 1P, CPU won → taunt voice FIRST, then a rotated loss-sting AFTER the voice (no win cheer).
        // (Sin voz / Silencioso → PlayRandomSfxAfterVoiceAsync plays the sting immediately.)
        if (mode == GameMode.OnePlayer && winner is int cpuW && _session.Players[cpuW].IsCpu)
        {
            if (VoiceOn) await Taunt("cpu-win", CpuTauntLines.CpuWin(EffectiveLevel()), interrupt: true);
            await _audio.PlayRandomSfxAfterVoiceAsync(AudioKeys.LossSting);
            return;
        }

        // Someone won (1P human, or any 2P win). Pick the closing line by how big a streak this win broke.
        if (winner is int)
        {
            bool bigBreak = _session.BrokenStreakLength >= CpuTauntPolicy.BigBreakThreshold;
            if (VoiceOn)
            {
                if (bigBreak)
                    await Taunt("streak-break-big", AudioKeys.StreakBreakBig, interrupt: true);   // any mode, +3
                else if (mode == GameMode.OnePlayer && _session.BrokenStreakLength >= CpuTauntPolicy.BreakThreshold)
                    await Taunt("streak-break", AudioKeys.StreakBreak, interrupt: true);            // 1P, broke a 2
                else if (mode == GameMode.OnePlayer)
                    await Taunt("beat-cpu", AudioKeys.BeatCpu, interrupt: true);
                else
                    await _audio.PlayRandomVoiceAsync(AudioKeys.VictoryV, interrupt: true);
            }
            // Closing SFX (plays even if voice is muted): big break gets the special "aleluya" after the voice;
            // everyone else gets the usual win cheer.
            if (bigBreak)
                await _audio.PlaySfxAfterVoiceAsync(AudioKeys.StreakBreakBigSting);
            else
                await _audio.PlayRandomSfxAfterVoiceAsync(AudioKeys.WinSfx);
            return;
        }

        // Draw (either mode): voice FIRST, then the draw sting AFTER it finishes.
        if (VoiceOn) await _audio.PlayRandomVoiceAsync(AudioKeys.DrawV, interrupt: true);
        await _audio.PlaySfxAfterVoiceAsync(AudioKeys.DrawSfx);
    }

    public void Dispose()
    {
        _session.GameStarted -= OnGameStarted;
        _session.RoundStarted -= OnRoundStarted;
        _session.ChipDropped -= OnChipDropped;
        _session.TurnChanged -= OnTurnChanged;
        _session.ColumnFull  -= OnColumnFull;
        _session.ThreatRaised -= OnThreat;
        _session.IdleNudged  -= OnIdle;
        _session.MatchEnded  -= OnMatchEnded;
        _session.AudioStopRequested -= OnAudioStopRequested;
    }
}
