using Connect4HoopsArcade.Core.Primitives;

namespace Connect4HoopsArcade.Core.Board;

/// <summary>7×6 Connect-4 grid. Indexed [col, row] with row 0 = bottom.</summary>
public sealed class GameBoard
{
    public const int Columns = 7;
    public const int Rows = 6;

    private readonly Cell[,] _cells;

    public GameBoard() => _cells = new Cell[Columns, Rows];
    private GameBoard(Cell[,] cells) => _cells = cells;

    public Cell this[int col, int row] => _cells[col, row];

    /// <summary>First empty row (0 = bottom) in a column, or -1 if full.</summary>
    public int LowestRow(int col)
    {
        for (int r = 0; r < Rows; r++)
            if (_cells[col, r] == Cell.Empty) return r;
        return -1;
    }

    public bool IsColumnFull(int col) => LowestRow(col) < 0;

    public bool IsBoardFull()
    {
        for (int c = 0; c < Columns; c++)
            if (_cells[c, Rows - 1] == Cell.Empty) return false;
        return true;
    }

    /// <summary>Places a cell at the lowest free row of the column. Returns the row, or -1 if full.</summary>
    public int Drop(int col, Cell cell)
    {
        int r = LowestRow(col);
        if (r < 0) return -1;
        _cells[col, r] = cell;
        return r;
    }

    public GameBoard Clone() => new((Cell[,])_cells.Clone());
}
