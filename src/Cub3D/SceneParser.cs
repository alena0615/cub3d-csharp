using System.Globalization;

namespace Cub3D;

public static class SceneParser
{
    private const string MapChars = " 01NSEWDX";
    private static readonly string[] RequiredIds = ["NO", "SO", "WE", "EA", "F", "C"];
    private static readonly string[] OptionalIds = ["DO", "SP"];

    public static Scene ParseFile(string path, bool checkTextures = true)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!path.EndsWith(".cub", StringComparison.OrdinalIgnoreCase))
        {
            throw new SceneFormatError($"файл сцены должен иметь расширение .cub: {path}");
        }

        string text = File.ReadAllText(path);
        string baseDir = Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".";
        return Parse(text, baseDir, checkTextures);
    }

    public static Scene Parse(string text, string? baseDir = null, bool checkTextures = true)
    {
        ArgumentNullException.ThrowIfNull(text);
        string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        var colors = new Dictionary<string, Rgb>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        int i = 0;
        for (; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd('\r');
            int lineNo = i + 1;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (IsMapLine(line))
            {
                string[] missing = RequiredIds.Where(id => !seen.Contains(id)).ToArray();
                if (missing.Length > 0)
                {
                    throw SceneFormatError.AtLine(
                        lineNo,
                        $"карта начинается до того, как заданы все элементы (не заданы: {string.Join(", ", missing)})");
                }

                break;
            }

            string trimmed = line.Trim();
            int space = trimmed.IndexOfAny([' ', '\t']);
            string id = space < 0 ? trimmed : trimmed[..space];
            string value = space < 0 ? string.Empty : trimmed[(space + 1)..].Trim();

            if (!RequiredIds.Contains(id) && !OptionalIds.Contains(id))
            {
                throw SceneFormatError.AtLine(lineNo, $"неизвестный идентификатор '{id}'");
            }

            if (!seen.Add(id))
            {
                throw SceneFormatError.AtLine(lineNo, $"элемент {id} задан повторно");
            }

            if (value.Length == 0)
            {
                throw SceneFormatError.AtLine(lineNo, $"у элемента {id} не указано значение");
            }

            if (id is "F" or "C")
            {
                colors[id] = ParseColor(value, lineNo);
            }
            else
            {
                paths[id] = ResolveTexture(value, baseDir, lineNo, checkTextures);
            }
        }

        if (i >= lines.Length)
        {
            string[] missing = RequiredIds.Where(id => !seen.Contains(id)).ToArray();
            if (missing.Length > 0)
            {
                throw new SceneFormatError($"не заданы элементы: {string.Join(", ", missing)}");
            }

            throw new SceneFormatError("в файле нет карты");
        }

        return ParseMap(lines, i, paths, colors);
    }

    public static Rgb ParseColor(string value, int lineNo = 0)
    {
        ArgumentNullException.ThrowIfNull(value);
        string[] parts = value.Split(',');
        if (parts.Length != 3)
        {
            throw ColorError(lineNo, $"цвет должен состоять из трёх чисел через запятую, получено '{value}'");
        }

        var c = new byte[3];
        for (int k = 0; k < 3; k++)
        {
            string p = parts[k].Trim();
            bool digitsOnly = p.Length is > 0 and <= 3 && p.All(char.IsAsciiDigit);
            if (!digitsOnly || !int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n > 255)
            {
                throw ColorError(lineNo, $"компонента цвета должна быть целым числом 0..255, получено '{p}'");
            }

            c[k] = (byte)n;
        }

        return new Rgb(c[0], c[1], c[2]);
    }

    private static SceneFormatError ColorError(int lineNo, string reason) =>
        lineNo > 0 ? SceneFormatError.AtLine(lineNo, reason) : new SceneFormatError(reason);

    private static bool IsMapLine(string line) => line.All(ch => MapChars.Contains(ch, StringComparison.Ordinal));

    private static string ResolveTexture(string value, string? baseDir, int lineNo, bool check)
    {
        if (!value.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            throw SceneFormatError.AtLine(lineNo, $"текстура должна быть файлом PNG: {value}");
        }

        if (!check)
        {
            return value;
        }

        if (File.Exists(value))
        {
            return value;
        }

        if (baseDir is not null && !Path.IsPathRooted(value))
        {
            string candidate = Path.Combine(baseDir, value);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw SceneFormatError.AtLine(lineNo, $"файл текстуры не найден: {value}");
    }

    private static Scene ParseMap(
        string[] lines,
        int start,
        Dictionary<string, string> paths,
        Dictionary<string, Rgb> colors)
    {
        int end = lines.Length;
        while (end > start && string.IsNullOrWhiteSpace(lines[end - 1]))
        {
            end--;
        }

        var rows = new List<string>();
        for (int i = start; i < end; i++)
        {
            string line = lines[i].TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                throw SceneFormatError.AtLine(i + 1, "пустая строка внутри карты");
            }

            rows.Add(line);
        }

        int height = rows.Count;
        int width = rows.Max(r => r.Length);
        var cells = new Cell[width * height];
        var sprites = new List<(int X, int Y)>();
        int startX = -1;
        int startY = -1;
        char startDir = ' ';

        for (int y = 0; y < height; y++)
        {
            int lineNo = start + y + 1;
            for (int x = 0; x < width; x++)
            {
                char ch = x < rows[y].Length ? rows[y][x] : ' ';
                Cell cell;
                switch (ch)
                {
                    case ' ':
                        cell = Cell.Void;
                        break;
                    case '0':
                        cell = Cell.Floor;
                        break;
                    case '1':
                        cell = Cell.Wall;
                        break;
                    case 'N' or 'S' or 'E' or 'W':
                        if (startX >= 0)
                        {
                            throw SceneFormatError.At(lineNo, x + 1, "игрок задан повторно");
                        }

                        (startX, startY, startDir) = (x, y, ch);
                        cell = Cell.Floor;
                        break;
                    case 'D':
                        if (!paths.ContainsKey("DO"))
                        {
                            throw SceneFormatError.At(lineNo, x + 1, "дверь 'D' на карте требует элемента DO");
                        }

                        cell = Cell.Door;
                        break;
                    case 'X':
                        if (!paths.ContainsKey("SP"))
                        {
                            throw SceneFormatError.At(lineNo, x + 1, "спрайт 'X' на карте требует элемента SP");
                        }

                        sprites.Add((x, y));
                        cell = Cell.Floor;
                        break;
                    default:
                        throw SceneFormatError.At(lineNo, x + 1, $"недопустимый символ '{ch}' в карте");
                }

                cells[(y * width) + x] = cell;
            }
        }

        if (startX < 0)
        {
            throw new SceneFormatError("на карте нет стартовой позиции игрока (N, S, E или W)");
        }

        var map = new GameMap(width, height, cells);
        CheckClosed(map, start + 1);

        return new Scene
        {
            North = paths["NO"],
            South = paths["SO"],
            West = paths["WE"],
            East = paths["EA"],
            Door = paths.GetValueOrDefault("DO"),
            Sprite = paths.GetValueOrDefault("SP"),
            Floor = colors["F"],
            Ceiling = colors["C"],
            Map = map,
            StartX = startX,
            StartY = startY,
            StartDirection = startDir,
            Sprites = sprites,
        };
    }

    private static void CheckClosed(GameMap map, int firstLineNo)
    {
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                Cell c = map[x, y];
                if (c is Cell.Wall or Cell.Void)
                {
                    continue;
                }

                int lineNo = firstLineNo + y;
                if (x == 0 || y == 0 || x == map.Width - 1 || y == map.Height - 1)
                {
                    throw new SceneFormatError(
                        $"карта не замкнута: клетка в строке {lineNo}, колонке {x + 1} лежит на краю карты");
                }

                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (map[x + dx, y + dy] == Cell.Void)
                        {
                            throw new SceneFormatError(
                                $"карта не замкнута: клетка в строке {lineNo}, колонке {x + 1} граничит с пустотой");
                        }
                    }
                }
            }
        }
    }
}
