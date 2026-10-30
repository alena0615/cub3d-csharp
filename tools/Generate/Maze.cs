using System.Text;

namespace Cub3D.Generate;

internal static class Maze
{
    private const int CellsX = 15, CellsY = 10;

    private const int Seed = 2026;

    private const string Header =
        """
        NO ../textures/north.png
        SO ../textures/south.png
        WE ../textures/west.png
        EA ../textures/east.png
        F 50,50,55
        C 100,140,180


        """;

    public static void Write(string path) =>
        File.WriteAllText(path, Header.Replace("\r\n", "\n", StringComparison.Ordinal) + string.Join('\n', Build()) + "\n");

    public static string[] Build()
    {
        const int w = (2 * CellsX) + 1, h = (2 * CellsY) + 1;
#pragma warning disable CA5394
        var rng = new Random(Seed);
        var grid = new char[h, w];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                grid[y, x] = '1';
            }
        }

        (int X, int Y) start = (0, CellsY - 1);
        Carve(grid, start.X, start.Y);
        var stack = new Stack<(int X, int Y)>();
        var seen = new HashSet<(int, int)> { start };
        stack.Push(start);
        (int Dx, int Dy)[] dirs = [(1, 0), (-1, 0), (0, 1), (0, -1)];
        while (stack.Count > 0)
        {
            (int x, int y) = stack.Peek();
            var next = new List<(int X, int Y)>();
            foreach ((int dx, int dy) in dirs)
            {
                (int nx, int ny) = (x + dx, y + dy);
                if (nx >= 0 && ny >= 0 && nx < CellsX && ny < CellsY && !seen.Contains((nx, ny)))
                {
                    next.Add((nx, ny));
                }
            }

            if (next.Count == 0)
            {
                stack.Pop();
                continue;
            }

            (int X, int Y) n = next[rng.Next(next.Count)];
            seen.Add(n);
            Carve(grid, n.X, n.Y);
            grid[y + n.Y + 1, x + n.X + 1] = '0';
            stack.Push(n);
        }

        var walls = new List<(int X, int Y)>();
        for (int y = 1; y < h - 1; y++)
        {
            for (int x = 1; x < w - 1; x++)
            {
                bool betweenX = x % 2 == 0 && y % 2 == 1 && grid[y, x - 1] == '0' && grid[y, x + 1] == '0';
                bool betweenY = x % 2 == 1 && y % 2 == 0 && grid[y - 1, x] == '0' && grid[y + 1, x] == '0';
                if (grid[y, x] == '1' && (betweenX || betweenY))
                {
                    walls.Add((x, y));
                }
            }
        }

        for (int i = 0; i < 14 && walls.Count > 0; i++)
        {
            int k = rng.Next(walls.Count);
            (int x, int y) = walls[k];
            grid[y, x] = '0';
            walls.RemoveAt(k);
        }
#pragma warning restore CA5394

        grid[h - 3, 1] = '0';
        grid[h - 2, 1] = 'N';

        var rows = new string[h];
        for (int y = 0; y < h; y++)
        {
            var sb = new StringBuilder(w);
            for (int x = 0; x < w; x++)
            {
                sb.Append(grid[y, x]);
            }

            rows[y] = sb.ToString();
        }

        return rows;
    }

    private static void Carve(char[,] grid, int cx, int cy) => grid[(2 * cy) + 1, (2 * cx) + 1] = '0';
}
