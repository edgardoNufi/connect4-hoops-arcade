namespace Connect4HoopsArcade.Core.Board;

/// <summary>A cell coordinate. Col 0-6 (left→right), Row 0-5 (0 = bottom).</summary>
public readonly record struct BoardPosition(int Col, int Row);
