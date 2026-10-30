using System.Diagnostics;
using System.Globalization;

namespace Cub3D;

public sealed record CliOptions
{
    public required string MapPath { get; init; }

    public int Width { get; init; } = 960;

    public int Height { get; init; } = 600;

    public string? Screenshot { get; init; }

    public int Bench { get; init; }

    public bool Minimap { get; init; }

    public bool Shading { get; init; }

    public static CliOptions? Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);
        string? map = null;
        int width = 960, height = 600, bench = 0;
        string? shot = null;
        bool minimap = false, shading = false;

        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            switch (a)
            {
                case "--width" or "--height" or "--bench":
                    if (i + 1 >= args.Length || !int.TryParse(args[++i], NumberStyles.None, CultureInfo.InvariantCulture, out int n))
                    {
                        return null;
                    }

                    if (a == "--bench")
                    {
                        if (n < 1)
                        {
                            return null;
                        }

                        bench = n;
                    }
                    else
                    {
                        if (n is < 64 or > 7680)
                        {
                            return null;
                        }

                        if (a == "--width")
                        {
                            width = n;
                        }
                        else
                        {
                            height = n;
                        }
                    }

                    break;
                case "--screenshot":
                    if (i + 1 >= args.Length)
                    {
                        return null;
                    }

                    shot = args[++i];
                    break;
                case "--minimap":
                    minimap = true;
                    break;
                case "--shading":
                    shading = true;
                    break;
                default:
                    if (a.StartsWith("--", StringComparison.Ordinal) || map is not null)
                    {
                        return null;
                    }

                    map = a;
                    break;
            }
        }

        return map is null
            ? null
            : new CliOptions
            {
                MapPath = map,
                Width = width,
                Height = height,
                Screenshot = shot,
                Bench = bench,
                Minimap = minimap,
                Shading = shading,
            };
    }
}

public static class Program
{
    private const string Usage =
        """
        Использование: cub3d <файл.cub> [параметры]
          --width N          ширина окна (по умолчанию 960)
          --height N         высота окна (по умолчанию 600)
          --screenshot F.png построить один кадр без окна и сохранить в PNG
          --bench N          построить N кадров без окна и напечатать время кадра
          --minimap          включить мини-карту сразу (в окне — клавиша M)
          --shading          включить затенение сразу (в окне — клавиша F)
        Управление: W/S — вперёд/назад, A/D — вбок, ←/→ и мышь — поворот,
                    Shift — бег, E — дверь, M — мини-карта, F — затенение, Esc — выход.
        """;

    public static int Main(string[] args)
    {
        CliOptions? opts = CliOptions.Parse(args);
        if (opts is null)
        {
            Console.Error.WriteLine(Usage);
            return 2;
        }

        try
        {
            return Run(opts);
        }
        catch (CubError e)
        {
            return Fail(e.Message);
        }
        catch (FileNotFoundException e)
        {
            return Fail($"файл не найден: {e.FileName ?? opts.MapPath}");
        }
        catch (DirectoryNotFoundException)
        {
            return Fail($"файл не найден: {opts.MapPath}");
        }
        catch (IOException e)
        {
            return Fail($"ошибка ввода-вывода: {e.Message}");
        }
        catch (UnauthorizedAccessException e)
        {
            return Fail($"нет доступа: {e.Message}");
        }
#pragma warning disable CA1031
        catch (Exception e)
#pragma warning restore CA1031
        {
            return Fail($"внутренняя ошибка: {e.Message}");
        }
    }

    private static int Fail(string reason)
    {
        Console.Error.WriteLine("Error");
        Console.Error.WriteLine(reason);
        return 1;
    }

    private static int Run(CliOptions opts)
    {
        Scene scene = SceneParser.ParseFile(opts.MapPath);
        TextureSet textures = TextureSet.Load(scene);
        var game = new Game(scene, textures, opts.Width, opts.Height);
        game.Options.Minimap = opts.Minimap;
        game.Options.Shading = opts.Shading;

        if (opts.Bench > 0)
        {
            RunBench(game, opts);
            return 0;
        }

        if (opts.Screenshot is not null)
        {
            game.Render();
            game.Frame.SavePng(opts.Screenshot);
            return 0;
        }

        GameWindow.Run(game);
        return 0;
    }

    private static void RunBench(Game game, CliOptions opts)
    {
        double turn = 2 * Math.PI / opts.Bench;
        for (int i = 0; i < 20; i++)
        {
            game.Render();
        }

        var times = new double[opts.Bench];
        var sw = new Stopwatch();
        for (int i = 0; i < opts.Bench; i++)
        {
            sw.Restart();
            game.Render();
            sw.Stop();
            times[i] = sw.Elapsed.TotalMilliseconds;
            game.Player.Rotate(turn);
        }

        double avg = times.Average();
        string report = string.Create(
            CultureInfo.InvariantCulture,
            $"{opts.MapPath} {opts.Width}x{opts.Height}: {opts.Bench} кадров, среднее {avg:F2} мс, лучшее {times.Min():F2} мс, худшее {times.Max():F2} мс, ≈ {1000 / avg:F0} FPS (без вывода на экран)");
        Console.WriteLine(report);
    }
}
