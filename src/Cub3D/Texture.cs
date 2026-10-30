namespace Cub3D;

public sealed class Texture
{
    private readonly uint[] _columns;

    public Texture(int width, int height, uint[] rowMajorPixels)
    {
        ArgumentNullException.ThrowIfNull(rowMajorPixels);
        if (rowMajorPixels.Length != width * height)
        {
            throw new ArgumentException("размер массива пикселей не совпадает с размером текстуры", nameof(rowMajorPixels));
        }

        Width = width;
        Height = height;
        _columns = new uint[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                _columns[(x * height) + y] = rowMajorPixels[(y * width) + x];
            }
        }
    }

    public int Width { get; }

    public int Height { get; }

    public uint this[int x, int y] => _columns[(x * Height) + y];

    public ReadOnlySpan<uint> Column(int x) => _columns.AsSpan(x * Height, Height);

    public static Texture Load(string path)
    {
        (int w, int h, uint[] pixels) = Png.Load(path);
        return new Texture(w, h, pixels);
    }

    public static Texture Solid(int width, int height, uint color)
    {
        var p = new uint[width * height];
        Array.Fill(p, color);
        return new Texture(width, height, p);
    }
}

public sealed class TextureSet
{
    public required Texture North { get; init; }

    public required Texture South { get; init; }

    public required Texture West { get; init; }

    public required Texture East { get; init; }

    public Texture? Door { get; init; }

    public Texture? Sprite { get; init; }

    public static TextureSet Load(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return new TextureSet
        {
            North = Texture.Load(scene.North),
            South = Texture.Load(scene.South),
            West = Texture.Load(scene.West),
            East = Texture.Load(scene.East),
            Door = scene.Door is null ? null : Texture.Load(scene.Door),
            Sprite = scene.Sprite is null ? null : Texture.Load(scene.Sprite),
        };
    }

    public Texture ForFacing(Facing facing) => facing switch
    {
        Facing.North => North,
        Facing.South => South,
        Facing.West => West,
        _ => East,
    };
}
