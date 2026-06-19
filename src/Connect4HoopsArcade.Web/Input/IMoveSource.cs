namespace Connect4HoopsArcade.Web.Input;

/// <summary>A producer of column triggers (clicks, keyboard, or physical sensors).</summary>
public interface IMoveSource
{
    /// <summary>Raised with a 0-based column index (0-6) when this source detects a drop.</summary>
    event Action<int>? ColumnTriggered;
}
