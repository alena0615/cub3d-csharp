namespace Cub3D.Generate;

internal static class Program
{
    public static int Main(string[] args)
    {
        string what = args.Length > 0 ? args[0] : "all";
        if (what is not ("all" or "textures" or "maze"))
        {
            Console.Error.WriteLine("Использование: dotnet run --project tools/Generate [textures|maze]");
            return 2;
        }

        string? root = FindRoot();
        if (root is null)
        {
            Console.Error.WriteLine("Error");
            Console.Error.WriteLine("не найден корень репозитория (файл Cub3D.sln): запустите из папки проекта");
            return 1;
        }

        if (what is "all" or "textures")
        {
            string dir = Path.Combine(root, "textures");
            Textures.WriteAll(dir);
            Console.WriteLine($"текстуры: {dir}");
        }

        if (what is "all" or "maze")
        {
            string path = Path.Combine(root, "maps", "maze.cub");
            Maze.Write(path);
            Console.WriteLine($"лабиринт: {path}");
        }

        return 0;
    }

    private static string? FindRoot()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Cub3D.sln")))
            {
                return dir.FullName;
            }
        }

        return null;
    }
}
