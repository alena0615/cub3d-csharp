namespace Cub3D.Tests;

internal static class TestData
{
    public static readonly string Root = FindRoot();

    public static string MapPath(string name) => Path.Combine(Root, "maps", name);

    public static string BadMapPath(string name) => Path.Combine(Root, "maps", "bad", name);

    public static string TexturePath(string name) => Path.Combine(Root, "textures", name);

    public static Scene SceneFromRows(int startX, int startY, char dir, params string[] rows) => new()
    {
        North = "north.png",
        South = "south.png",
        West = "west.png",
        East = "east.png",
        Floor = new Rgb(10, 20, 30),
        Ceiling = new Rgb(200, 210, 220),
        Map = GameMap.FromRows(rows),
        StartX = startX,
        StartY = startY,
        StartDirection = dir,
    };

    public static readonly uint NorthColor = Pixel.Pack(255, 0, 0);
    public static readonly uint SouthColor = Pixel.Pack(0, 255, 0);
    public static readonly uint WestColor = Pixel.Pack(0, 0, 255);
    public static readonly uint EastColor = Pixel.Pack(255, 255, 0);

    public static TextureSet SolidTextures(Texture? sprite = null) => new()
    {
        North = Texture.Solid(64, 64, NorthColor),
        South = Texture.Solid(64, 64, SouthColor),
        West = Texture.Solid(64, 64, WestColor),
        East = Texture.Solid(64, 64, EastColor),
        Sprite = sprite,
    };

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Cub3D.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("не найден корень репозитория (Cub3D.sln)");
    }
}
