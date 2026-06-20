using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Web.Services;

/// <summary>Spanish display names for the CPU difficulty ladder.</summary>
public static class CpuLevelLabels
{
    public static string Name(CpuDifficulty d) => d switch
    {
        CpuDifficulty.Novato => "Novato",
        CpuDifficulty.Principiante => "Principiante",
        CpuDifficulty.Amateur => "Amateur",
        CpuDifficulty.Titular => "Titular",
        CpuDifficulty.Estrella => "Estrella",
        _ => "MVP",
    };
}
