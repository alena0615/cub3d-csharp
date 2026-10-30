namespace Cub3D.Generate;

internal static class Textures
{
    private const int Size = 64;

    private static readonly (byte R, byte G, byte B) Red = (178, 58, 44);
    private static readonly (byte R, byte G, byte B) Blue = (52, 92, 176);
    private static readonly (byte R, byte G, byte B) Mortar = (205, 200, 190);

    public static void WriteAll(string dir)
    {
        Directory.CreateDirectory(dir);
        Bricks(Red).SavePng(Path.Combine(dir, "north.png"));
        Bricks(Red).SavePng(Path.Combine(dir, "south.png"));
        Bricks(Blue).SavePng(Path.Combine(dir, "west.png"));
        Bricks(Blue).SavePng(Path.Combine(dir, "east.png"));
        Door().SavePng(Path.Combine(dir, "door.png"));
        Plant().SavePng(Path.Combine(dir, "sprite.png"));
    }

    public static Canvas Bricks((byte R, byte G, byte B) color)
    {
        var c = new Canvas(Size, Size, Rgb(Mortar));
        const int bh = 16, bw = 32;
        for (int row = 0; row < Size / bh; row++)
        {
            int off = bw / 2 * (row % 2);
            int y0 = (row * bh) + 1, y1 = (row * bh) + bh - 2;
            for (int x0 = -bw; x0 < Size + bw; x0 += bw)
            {
                int a = x0 + off + 1, b = x0 + off + bw - 2;
                c.FillRect(a, y0, b, y1, Rgb(color));
                c.FillRect(a, y0, b, y0, Rgb(Shade(color, 1.18)));
                c.FillRect(a, y1, b, y1, Rgb(Shade(color, 0.78)));
            }
        }

        return c;
    }

    public static Canvas Door()
    {
        const int big = 256;
        var c = new Canvas(big, big, Pixel.Pack(70, 44, 26));
        (byte, byte, byte) woodA = (150, 96, 52), woodB = (132, 82, 44);

        for (int i = 0, x = 16; x < 240; i++, x += 32)
        {
            c.FillRect(x, 16, x + 31, 239, Rgb(i % 2 == 0 ? woodA : woodB));
            c.Line(x, 16, x, 239, 2, Rgb(Shade(woodA, 0.6)));
        }

        c.RectOutline(0, 0, big - 1, big - 1, 16, Pixel.Pack(84, 52, 30));
        c.RectOutline(14, 14, big - 15, big - 15, 3, Pixel.Pack(52, 32, 18));

        foreach ((int y0, int y1) in new[] { (40, 116), (140, 216) })
        {
            c.RectOutline(44, y0, 212, y1, 4, Rgb(Shade(woodA, 0.55)));
            c.Line(48, y0 + 4, 208, y0 + 4, 3, Rgb(Shade(woodA, 1.25)));
            c.Line(48, y0 + 4, 48, y1 - 4, 3, Rgb(Shade(woodA, 1.25)));
        }

        uint iron = Pixel.Pack(58, 58, 64), ironHi = Pixel.Pack(120, 120, 130), rivetEdge = Pixel.Pack(30, 30, 34);
        foreach (int y in new[] { 52, 188 })
        {
            c.FillRoundedRect(16, y, 120, y + 18, 8, iron);
            c.Line(20, y + 4, 112, y + 4, 2, ironHi);
            foreach (int x in new[] { 36, 66, 96 })
            {
                c.FillEllipse(x - 5, y + 4, x + 5, y + 14, ironHi);
                c.EllipseOutline(x - 5, y + 4, x + 5, y + 14, 1, rivetEdge);
            }
        }

        c.FillEllipse(186, 112, 216, 142, Pixel.Pack(200, 160, 60));
        c.EllipseOutline(186, 112, 216, 142, 2, Pixel.Pack(110, 80, 20));
        c.EllipseOutline(178, 132, 224, 178, 6, Pixel.Pack(210, 170, 70));
        uint hole = Pixel.Pack(20, 14, 8);
        c.FillEllipse(197, 186, 205, 194, hole);
        c.FillPolygon([(199, 192), (203, 192), (205, 204), (197, 204)], hole);

        return c.Downscale(Size, Size);
    }

    public static Canvas Plant()
    {
        const int big = 256;
        var c = new Canvas(big, big, 0);
        uint pot = Pixel.Pack(178, 84, 44), potRim = Pixel.Pack(198, 100, 56), potDark = Pixel.Pack(110, 50, 25);
        uint leaf = Pixel.Pack(60, 160, 70), leafDark = Pixel.Pack(40, 110, 50);

        (double, double)[] body = [(84, 176), (172, 176), (158, 252), (98, 252)];
        c.FillPolygon(body, pot);
        c.PolygonOutline(body, potDark);
        c.FillRect(76, 164, 180, 182, potRim);
        c.RectOutline(76, 164, 180, 182, 1, potDark);
        c.Line(128, 166, 128, 92, 6, leafDark);
        foreach ((int cx, int cy, int r) in new[] { (128, 76, 34), (90, 104, 26), (166, 104, 26), (104, 60, 22), (152, 60, 22), (128, 120, 26) })
        {
            c.FillEllipse(cx - r, cy - r, cx + r, cy + r, leaf);
            c.EllipseOutline(cx - r, cy - r, cx + r, cy + r, 3, leafDark);
        }

        Canvas small = c.Downscale(40, 40);
        var result = new Canvas(Size, Size, 0);
        result.Composite(small, 12, Size - 40);
        return result;
    }

    private static uint Rgb((byte R, byte G, byte B) c) => Pixel.Pack(c.R, c.G, c.B);

    private static (byte R, byte G, byte B) Shade((byte R, byte G, byte B) c, double k) =>
        (Mul(c.R, k), Mul(c.G, k), Mul(c.B, k));

    private static byte Mul(byte v, double k) => (byte)Math.Clamp((int)(v * k), 0, 255);
}
