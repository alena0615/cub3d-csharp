namespace Cub3D;

public enum Cell
{
    Void,

    Floor,

    Wall,

    Door,
}

public enum Facing
{
    North,
    South,
    West,
    East,
}

public sealed class GameMap
{
    private readonly Cell[] _cells;

    public GameMap(int width, int height, Cell[] cells)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Length != width * height)
        {
            throw new ArgumentException("размер массива клеток не совпадает с размером карты", nameof(cells));
        }

        Width = width;
        Height = height;
        _cells = cells;
    }

    public int Width { get; }

    public int Height { get; }

    public Cell this[int x, int y] =>
        x >= 0 && y >= 0 && x < Width && y < Height ? _cells[(y * Width) + x] : Cell.Void;

    public static GameMap FromRows(params string[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        int h = rows.Length;
        int w = rows.Length == 0 ? 0 : rows.Max(r => r.Length);
        var cells = new Cell[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                char c = x < rows[y].Length ? rows[y][x] : ' ';
                cells[(y * w) + x] = c switch
                {
                    '1' => Cell.Wall,
                    'D' => Cell.Door,
                    ' ' => Cell.Void,
                    _ => Cell.Floor,
                };
            }
        }

        return new GameMap(w, h, cells);
    }
}

public sealed record Scene
{
    public required string North { get; init; }

    public required string South { get; init; }

    public required string West { get; init; }

    public required string East { get; init; }

    public string? Door { get; init; }

    public string? Sprite { get; init; }

    public required Rgb Floor { get; init; }

    public required Rgb Ceiling { get; init; }

    public required GameMap Map { get; init; }

    public required int StartX { get; init; }

    public required int StartY { get; init; }

    public required char StartDirection { get; init; }

    public IReadOnlyList<(int X, int Y)> Sprites { get; init; } = [];
}
