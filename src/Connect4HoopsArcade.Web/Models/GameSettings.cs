using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Web.Models;

public sealed class GameSettings
{
    public int MusicVolume { get; set; } = 20;   // background level — music loops under voices/SFX
    public int SfxVolume { get; set; } = 80;
    public int NarratorVolume { get; set; } = 60;
    public bool VoicesEnabled { get; set; } = true;
    public AnimationSpeed Speed { get; set; } = AnimationSpeed.Normal;
    public NarratorTone Tone { get; set; } = NarratorTone.Familiar;
    public PlayMode Mode { get; set; } = PlayMode.Digital;
    public CpuDifficulty CpuLevel { get; set; } = CpuDifficulty.Amateur;
}
