using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Core.Catalog;

public sealed record FaceOption(FaceId Id, string Label);

public static class FaceCatalog
{
    public static readonly IReadOnlyList<FaceOption> All = new[]
    {
        new FaceOption(FaceId.Happy,     "Feliz"),
        new FaceOption(FaceId.Confident, "Confiado"),
        new FaceOption(FaceId.Serious,   "Serio"),
        new FaceOption(FaceId.Surprised, "Sorprendido"),
        new FaceOption(FaceId.Angry,     "Enojado"),
    };
}
