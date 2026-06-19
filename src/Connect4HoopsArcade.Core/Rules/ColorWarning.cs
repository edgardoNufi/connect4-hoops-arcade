namespace Connect4HoopsArcade.Core.Rules;

public enum ColorWarning { None, Same, Similar }

public static class ColorWarningMessages
{
    public static string? Message(ColorWarning w) => w switch
    {
        ColorWarning.Same    => "Mismo color: elige uno distinto para cada jugador.",
        ColorWarning.Similar => "Colores muy parecidos: podrían confundirse en el tablero.",
        _ => null,
    };
}
