namespace Cub3D.Generate;

internal sealed class Canvas
{
    private readonly uint[] _pixels;

    public Canvas(int width, int height, uint background)
    {
        Width = width;
        Height = height;
        _pixels = new uint[width * height];
        Array.Fill(_pixels, background);
    }

    public int Width { get; }

    public int Height { get; }

    public uint[] Pixels => _pixels;

    public uint this[int x, int y] => _pixels[(y * Width) + x];

    public void Set(int x, int y, uint color)
    {
        if ((uint)x < (uint)Width && (uint)y < (uint)Height)
        {
            _pixels[(y * Width) + x] = color;
        }
    }

    public void FillRect(int x0, int y0, int x1, int y1, uint color)
    {
        for (int y = Math.Max(0, y0); y <= Math.Min(Height - 1, y1); y++)
        {
            for (int x = Math.Max(0, x0); x <= Math.Min(Width - 1, x1); x++)
            {
                _pixels[(y * Width) + x] = color;
            }
        }
    }

    public void RectOutline(int x0, int y0, int x1, int y1, int width, uint color)
    {
        FillRect(x0, y0, x1, y0 + width - 1, color);
        FillRect(x0, y1 - width + 1, x1, y1, color);
        FillRect(x0, y0, x0 + width - 1, y1, color);
        FillRect(x1 - width + 1, y0, x1, y1, color);
    }

    public void Line(double x0, double y0, double x1, double y1, double width, uint color)
    {
        double r = Math.Max(0.5, width / 2);
        int minX = (int)Math.Floor(Math.Min(x0, x1) - r), maxX = (int)Math.Ceiling(Math.Max(x0, x1) + r);
        int minY = (int)Math.Floor(Math.Min(y0, y1) - r), maxY = (int)Math.Ceiling(Math.Max(y0, y1) + r);
        double dx = x1 - x0, dy = y1 - y0, len2 = (dx * dx) + (dy * dy);
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                double t = len2 == 0 ? 0 : Math.Clamp((((x - x0) * dx) + ((y - y0) * dy)) / len2, 0, 1);
                double ex = x0 + (t * dx) - x, ey = y0 + (t * dy) - y;
                if ((ex * ex) + (ey * ey) <= r * r)
                {
                    Set(x, y, color);
                }
            }
        }
    }

    public void FillEllipse(double x0, double y0, double x1, double y1, uint color)
    {
        double cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, rx = (x1 - x0 + 1) / 2, ry = (y1 - y0 + 1) / 2;
        for (int y = (int)Math.Floor(y0); y <= (int)Math.Ceiling(y1); y++)
        {
            for (int x = (int)Math.Floor(x0); x <= (int)Math.Ceiling(x1); x++)
            {
                if (InEllipse(x, y, cx, cy, rx, ry))
                {
                    Set(x, y, color);
                }
            }
        }
    }

    public void EllipseOutline(double x0, double y0, double x1, double y1, double width, uint color)
    {
        double cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, rx = (x1 - x0 + 1) / 2, ry = (y1 - y0 + 1) / 2;
        for (int y = (int)Math.Floor(y0); y <= (int)Math.Ceiling(y1); y++)
        {
            for (int x = (int)Math.Floor(x0); x <= (int)Math.Ceiling(x1); x++)
            {
                if (InEllipse(x, y, cx, cy, rx, ry) && !InEllipse(x, y, cx, cy, rx - width, ry - width))
                {
                    Set(x, y, color);
                }
            }
        }
    }

    public void FillRoundedRect(int x0, int y0, int x1, int y1, int radius, uint color)
    {
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                double nx = Math.Clamp(x, x0 + radius, x1 - radius), ny = Math.Clamp(y, y0 + radius, y1 - radius);
                if (((x - nx) * (x - nx)) + ((y - ny) * (y - ny)) <= radius * radius)
                {
                    Set(x, y, color);
                }
            }
        }
    }

    public void FillPolygon(IReadOnlyList<(double X, double Y)> points, uint color)
    {
        int minX = (int)Math.Floor(points.Min(p => p.X)), maxX = (int)Math.Ceiling(points.Max(p => p.X));
        int minY = (int)Math.Floor(points.Min(p => p.Y)), maxY = (int)Math.Ceiling(points.Max(p => p.Y));
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (Inside(points, x, y))
                {
                    Set(x, y, color);
                }
            }
        }
    }

    public void PolygonOutline(IReadOnlyList<(double X, double Y)> points, uint color)
    {
        for (int i = 0; i < points.Count; i++)
        {
            (double ax, double ay) = points[i];
            (double bx, double by) = points[(i + 1) % points.Count];
            Line(ax, ay, bx, by, 1, color);
        }
    }

    public Canvas Downscale(int width, int height)
    {
        var result = new Canvas(width, height, 0);
        double sx = (double)Width / width, sy = (double)Height / height;
        for (int ty = 0; ty < height; ty++)
        {
            for (int tx = 0; tx < width; tx++)
            {
                int xa = (int)(tx * sx), xb = Math.Max(xa + 1, (int)((tx + 1) * sx));
                int ya = (int)(ty * sy), yb = Math.Max(ya + 1, (int)((ty + 1) * sy));
                double r = 0, g = 0, b = 0, a = 0;
                int n = 0;
                for (int y = ya; y < yb; y++)
                {
                    for (int x = xa; x < xb; x++)
                    {
                        uint p = this[x, y];
                        double pa = Pixel.A(p);
                        r += Pixel.R(p) * pa;
                        g += Pixel.G(p) * pa;
                        b += Pixel.B(p) * pa;
                        a += pa;
                        n++;
                    }
                }

                result.Set(tx, ty, a == 0
                    ? 0
                    : Pixel.Pack(ToByte(r / a), ToByte(g / a), ToByte(b / a), ToByte(a / n)));
            }
        }

        return result;
    }

    public void Composite(Canvas src, int ox, int oy)
    {
        for (int y = 0; y < src.Height; y++)
        {
            for (int x = 0; x < src.Width; x++)
            {
                uint s = src[x, y];
                double a = Pixel.A(s) / 255.0;
                if (a <= 0)
                {
                    continue;
                }

                uint d = this[ox + x, oy + y];
                double da = Pixel.A(d) / 255.0;
                double oa = a + (da * (1 - a));
                byte Mix(byte sc, byte dc) => ToByte(((sc * a) + (dc * da * (1 - a))) / oa);
                Set(ox + x, oy + y, Pixel.Pack(Mix(Pixel.R(s), Pixel.R(d)), Mix(Pixel.G(s), Pixel.G(d)), Mix(Pixel.B(s), Pixel.B(d)), ToByte(oa * 255)));
            }
        }
    }

    public void SavePng(string path) => Png.Save(path, Width, Height, _pixels);

    private static bool InEllipse(int x, int y, double cx, double cy, double rx, double ry)
    {
        if (rx <= 0 || ry <= 0)
        {
            return false;
        }

        double dx = (x - cx) / rx, dy = (y - cy) / ry;
        return (dx * dx) + (dy * dy) <= 1;
    }

    private static bool Inside(IReadOnlyList<(double X, double Y)> pts, double px, double py)
    {
        bool inside = false;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
        {
            (double xi, double yi) = pts[i];
            (double xj, double yj) = pts[j];
            if ((yi > py) != (yj > py) && px < ((xj - xi) * (py - yi) / (yj - yi)) + xi)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static byte ToByte(double v) => (byte)Math.Clamp(Math.Round(v), 0, 255);
}
