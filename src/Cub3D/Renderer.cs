namespace Cub3D;

public readonly record struct Clip(int X0, int Y0, int X1, int Y1);

public sealed class FrameBuffer
{
    public FrameBuffer(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "размер кадра должен быть положительным");
        }

        Width = width;
        Height = height;
        Pixels = new uint[width * height];
    }

    public int Width { get; }

    public int Height { get; }

#pragma warning disable CA1819
    public uint[] Pixels { get; }
#pragma warning restore CA1819

    public uint this[int x, int y] => Pixels[(y * Width) + x];

    public void Set(int x, int y, uint color)
    {
        if ((uint)x < (uint)Width && (uint)y < (uint)Height)
        {
            Pixels[(y * Width) + x] = color;
        }
    }

    public void FillRect(int x0, int y0, int w, int h, uint color)
    {
        int x1 = Math.Min(Width, x0 + w);
        int y1 = Math.Min(Height, y0 + h);
        x0 = Math.Max(0, x0);
        y0 = Math.Max(0, y0);
        for (int y = y0; y < y1; y++)
        {
            Pixels.AsSpan((y * Width) + x0, Math.Max(0, x1 - x0)).Fill(color);
        }
    }

    public void Line(int x0, int y0, int x1, int y1, uint color) =>
        Line(x0, y0, x1, y1, color, new Clip(0, 0, Width - 1, Height - 1));

    public void Line(int x0, int y0, int x1, int y1, uint color, Clip clip)
    {
        int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        for (int guard = 0; guard < 10000; guard++)
        {
            if (x0 >= clip.X0 && x0 <= clip.X1 && y0 >= clip.Y0 && y0 <= clip.Y1)
            {
                Set(x0, y0, color);
            }

            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x0 += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    public void SavePng(string path) => Png.Save(path, Width, Height, Pixels);
}

public sealed class RenderOptions
{
    public bool Shading { get; set; }

    public bool Minimap { get; set; }
}

public sealed class Renderer
{
    private static readonly (double X, double Y, double H) Light = Normalize(-0.35, -0.45, 0.82);

    private double[] _zBuffer = [];
    private float[] _pixelDepth = [];
    private double[] _parts = [];
    private RayHit[] _hits = [];

    public ReadOnlySpan<double> ZBuffer => _zBuffer;

    public static double ShadeFactor(double distance, int side)
    {
        double k = 1.0 / (1.0 + (0.12 * distance));
        return side == 1 ? k * 0.8 : k;
    }

    public void Render(World world, Player player, Scene scene, TextureSet textures, FrameBuffer frame, RenderOptions options, IReadOnlyList<PlacedModel>? models = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(textures);
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(options);

        int w = frame.Width;
        int h = frame.Height;
        if (_zBuffer.Length != w)
        {
            _zBuffer = new double[w];
            _hits = new RayHit[w];
        }

        int half = h / 2;
        frame.Pixels.AsSpan(0, half * w).Fill(scene.Ceiling.Packed);
        frame.Pixels.AsSpan(half * w).Fill(scene.Floor.Packed);

        for (int x = 0; x < w; x++)
        {
            RayHit hit = Raycaster.CastColumn(world, player, x, w);
            _hits[x] = hit;
            _zBuffer[x] = hit.Distance;
            if (hit.Hit)
            {
                DrawWallColumn(frame, x, hit, PickTexture(textures, hit), options.Shading);
            }
        }

        bool hasModels = models is { Count: > 0 };
        bool hasSprites = textures.Sprite is not null && scene.Sprites.Count > 0;
        bool useDepth = hasModels && hasSprites;
        if (useDepth)
        {
            if (_pixelDepth.Length != w * h)
            {
                _pixelDepth = new float[w * h];
            }

            _pixelDepth.AsSpan().Fill(float.PositiveInfinity);
        }

        if (hasModels)
        {
            foreach (PlacedModel m in models!)
            {
                DrawModel(frame, player, m, options.Shading, useDepth);
            }
        }

        if (hasSprites)
        {
            DrawSprites(frame, player, scene, textures.Sprite!, options.Shading, useDepth);
        }

        if (options.Minimap)
        {
            DrawMinimap(frame, world, player, scene);
        }
    }

    private static Texture PickTexture(TextureSet textures, RayHit hit) =>
        hit.IsDoor && textures.Door is not null ? textures.Door : textures.ForFacing(hit.Facing);

    private static void DrawWallColumn(FrameBuffer frame, int x, RayHit hit, Texture tex, bool shading)
    {
        int h = frame.Height;
        int w = frame.Width;
        double lineH = Raycaster.LineHeight(h, hit.Distance);
        double top = (h / 2.0) - (lineH / 2.0);

        int drawStart = (int)Math.Max(0, Math.Ceiling(top));
        int drawEnd = (int)Math.Min(h, Math.Ceiling(top + lineH));

        int texX = hit.IsDoor
            ? Math.Clamp((int)(hit.WallX * tex.Width), 0, tex.Width - 1)
            : Raycaster.TextureColumn(hit.WallX, hit.Facing, tex.Width);
        ReadOnlySpan<uint> column = tex.Column(texX);
        int texH = tex.Height;
        double step = texH / lineH;

        double texPos = (drawStart - top) * step;
        int k256 = shading ? (int)(ShadeFactor(hit.Distance, hit.Side) * 256) : 256;

        uint[] px = frame.Pixels;
        int idx = (drawStart * w) + x;
        for (int y = drawStart; y < drawEnd; y++)
        {
            int texY = Math.Min((int)texPos, texH - 1);
            texPos += step;
            uint c = column[texY];
            px[idx] = k256 == 256 ? c : Pixel.Shade(c, k256);
            idx += w;
        }
    }

    private void DrawSprites(FrameBuffer frame, Player player, Scene scene, Texture tex, bool shading, bool checkPixelDepth)
    {
        int w = frame.Width;
        int h = frame.Height;
        var order = scene.Sprites
            .OrderByDescending(s => Sq(s.X + 0.5 - player.PosX) + Sq(s.Y + 0.5 - player.PosY))
            .ToArray();

        double det = (player.PlaneX * player.DirY) - (player.DirX * player.PlaneY);
        double inv = 1.0 / det;
        foreach ((int cellX, int cellY) in order)
        {
            double sx = cellX + 0.5 - player.PosX;
            double sy = cellY + 0.5 - player.PosY;
            double depth = inv * ((-player.PlaneY * sx) + (player.PlaneX * sy));
            double tx = inv * ((player.DirY * sx) - (player.DirX * sy));
            if (depth <= 0.05)
            {
                continue;
            }

            int screenX = (int)((w / 2.0) * (1 + (tx / depth)));
            int size = (int)Math.Abs(h / depth);
            if (size <= 0)
            {
                continue;
            }

            int startY = Math.Max(0, (h / 2) - (size / 2));
            int endY = Math.Min(h, (h / 2) + (size / 2));
            int startX = screenX - (size / 2);
            int endX = Math.Min(w, screenX + (size / 2));
            int k256 = shading ? (int)(ShadeFactor(depth, 0) * 256) : 256;

            for (int stripe = Math.Max(0, startX); stripe < endX; stripe++)
            {
                if (depth >= _zBuffer[stripe])
                {
                    continue;
                }

                int texX = Math.Clamp((stripe - startX) * tex.Width / size, 0, tex.Width - 1);
                ReadOnlySpan<uint> col = tex.Column(texX);
                for (int y = startY; y < endY; y++)
                {
                    int d = (y - ((h / 2) - (size / 2))) * tex.Height / size;
                    uint c = col[Math.Clamp(d, 0, tex.Height - 1)];
                    int idx = (y * w) + stripe;
                    if (Pixel.A(c) < 128 || (checkPixelDepth && depth >= _pixelDepth[idx]))
                    {
                        continue;
                    }

                    frame.Pixels[idx] = k256 == 256 ? c : Pixel.Shade(c, k256);
                }
            }
        }
    }

    private void DrawModel(FrameBuffer frame, Player player, PlacedModel m, bool shading, bool writeDepth)
    {
        int w = frame.Width;
        int h = frame.Height;
        Model3D model = m.Model;
        int n = model.Parts.Count;
        const int Stride = 16;
        if (_parts.Length < n * Stride)
        {
            _parts = new double[n * Stride];
        }

        double s = model.Scale;
        for (int i = 0; i < n; i++)
        {
            Ellipsoid e = model.Parts[i];
            (double cx, double cy, double ch) = m.ToWorld(e.X, e.Y, e.Z);
            double dx = player.PosX - cx, dy = player.PosY - cy, dh = 0.5 - ch;
            double ia = 1 / (s * e.Rx), ib = 1 / (s * e.Rz), ic = 1 / (s * e.Ry);
            int o = i * Stride;
            _parts[o] = ((dx * m.ForwardX) + (dy * m.ForwardY)) * ia;
            _parts[o + 1] = ((dx * m.SideX) + (dy * m.SideY)) * ib;
            _parts[o + 2] = dh * ic;
            _parts[o + 3] = ia;
            _parts[o + 4] = ib;
            _parts[o + 5] = ic;
            _parts[o + 11] = cx;
            _parts[o + 12] = cy;
            _parts[o + 13] = s * Math.Max(e.Rx, e.Rz);
            _parts[o + 14] = ch - (s * e.Ry);
            _parts[o + 15] = ch + (s * e.Ry);
        }

        double[] parts = _parts;
        double[] zBuffer = _zBuffer;
        float[]? pixelDepth = writeDepth ? _pixelDepth : null;
        Parallel.For(0, w, x => DrawModelColumn(frame, player, m, parts, zBuffer, pixelDepth, x, shading));
    }

    private static void DrawModelColumn(FrameBuffer frame, Player player, PlacedModel m, double[] parts, double[] zBuffer, float[]? pixelDepth, int x, bool shading)
    {
        const int Stride = 16;
        Model3D model = m.Model;
        int n = model.Parts.Count;
        int w = frame.Width;
        int h = frame.Height;
        (double rx, double ry) = Raycaster.RayDirection(player, Raycaster.CameraX(x, w));
        double zWall = zBuffer[x];

        if (!CircleSpan(player.PosX - m.X, player.PosY - m.Y, rx, ry, model.BoundingRadius, out double t0, out _) || t0 >= zWall)
        {
            return;
        }

        Span<double> col = stackalloc double[n * 5];
        Span<int> rowFrom = stackalloc int[n];
        Span<int> rowTo = stackalloc int[n];

        int yMin = h, yMax = -1;
        for (int i = 0; i < n; i++)
        {
            int o = i * Stride;
            rowFrom[i] = 1;
            rowTo[i] = 0;
            if (!CircleSpan(player.PosX - parts[o + 11], player.PosY - parts[o + 12], rx, ry, parts[o + 13], out double ta, out double tb)
                || ta >= zWall)
            {
                continue;
            }

            double near = Math.Max(ta, 0.02);
            int from = (int)Math.Floor(Math.Min(RowAt(h, parts[o + 15], near), RowAt(h, parts[o + 15], tb)));
            int to = (int)Math.Ceiling(Math.Max(RowAt(h, parts[o + 14], near), RowAt(h, parts[o + 14], tb)));
            rowFrom[i] = Math.Max(0, from);
            rowTo[i] = Math.Min(h - 1, to);
            yMin = Math.Min(yMin, rowFrom[i]);
            yMax = Math.Max(yMax, rowTo[i]);

            col[i * 5] = ((rx * m.ForwardX) + (ry * m.ForwardY)) * parts[o + 3];
            col[(i * 5) + 1] = ((rx * m.SideX) + (ry * m.SideY)) * parts[o + 4];
        }

        for (int y = yMin; y <= yMax; y++)
        {
            double k = ((h / 2.0) - y) / h;
            double best = zWall;
            int bestPart = -1;
            for (int i = 0; i < n; i++)
            {
                if (y < rowFrom[i] || y > rowTo[i])
                {
                    continue;
                }

                int o = i * Stride;
                int c5 = i * 5;
                double oa = parts[o], ob = parts[o + 1], oc = parts[o + 2];
                double da = col[c5], db = col[c5 + 1], dc = k * parts[o + 5];
                double a = (da * da) + (db * db) + (dc * dc);
                double b = (oa * da) + (ob * db) + (oc * dc);
                double c = (oa * oa) + (ob * ob) + (oc * oc) - 1;
                double disc = (b * b) - (a * c);
                if (disc < 0)
                {
                    continue;
                }

                double t = (-b - Math.Sqrt(disc)) / a;
                if (t > 1e-4 && t < best)
                {
                    best = t;
                    bestPart = i;
                    col[c5 + 2] = oa + (t * da);
                    col[c5 + 3] = ob + (t * db);
                    col[c5 + 4] = oc + (t * dc);
                }
            }

            if (bestPart < 0)
            {
                continue;
            }

            int q = bestPart * Stride;
            int b5 = bestPart * 5;
            double na = col[b5 + 2] * parts[q + 3], nb = col[b5 + 3] * parts[q + 4], nc = col[b5 + 4] * parts[q + 5];
            double nx = (na * m.ForwardX) + (nb * m.SideX);
            double ny = (na * m.ForwardY) + (nb * m.SideY);
            double len = Math.Sqrt((nx * nx) + (ny * ny) + (nc * nc));
            double lambert = Math.Max(0, ((nx * Light.X) + (ny * Light.Y) + (nc * Light.H)) / len);
            double bright = 0.42 + (0.58 * lambert);
            if (shading)
            {
                bright *= ShadeFactor(best, 0);
            }

            int idx = (y * w) + x;
            frame.Pixels[idx] = Pixel.Shade(model.Parts[bestPart].Color, (int)(bright * 256));
            if (pixelDepth is not null)
            {
                pixelDepth[idx] = (float)best;
            }
        }
    }

    private static bool CircleSpan(double fx, double fy, double rx, double ry, double r, out double t0, out double t1)
    {
        double qa = (rx * rx) + (ry * ry);
        double qb = (fx * rx) + (fy * ry);
        double qc = (fx * fx) + (fy * fy) - (r * r);
        double qd = (qb * qb) - (qa * qc);
        t0 = t1 = 0;
        if (qd < 0)
        {
            return false;
        }

        double sq = Math.Sqrt(qd);
        t0 = (-qb - sq) / qa;
        t1 = (-qb + sq) / qa;
        return t1 > 0.02;
    }

    private static double RowAt(int screenH, double hgt, double t) => (screenH / 2.0) - ((hgt - 0.5) * screenH / t);

    private static (double X, double Y, double H) Normalize(double x, double y, double z)
    {
        double len = Math.Sqrt((x * x) + (y * y) + (z * z));
        return (x / len, y / len, z / len);
    }

    private static double Sq(double v) => v * v;

    private void DrawMinimap(FrameBuffer frame, World world, Player player, Scene scene)
    {
        GameMap map = world.Map;
        int maxSide = Math.Min(frame.Width, frame.Height) / 3;
        const int minCell = 7;
        int cell = Math.Min(16, maxSide / Math.Max(map.Width, map.Height));
        int viewW = map.Width, viewH = map.Height, vx0 = 0, vy0 = 0;
        if (cell < minCell)
        {
            cell = minCell;
            viewW = Math.Min(map.Width, maxSide / cell);
            viewH = Math.Min(map.Height, maxSide / cell);
            vx0 = Math.Clamp((int)player.PosX - (viewW / 2), 0, map.Width - viewW);
            vy0 = Math.Clamp((int)player.PosY - (viewH / 2), 0, map.Height - viewH);
        }

        const int ox = 10, oy = 10;
        var clip = new Clip(ox, oy, ox + (viewW * cell) - 1, oy + (viewH * cell) - 1);
        int ToX(double wx) => ox + (int)Math.Floor((wx - vx0) * cell);
        int ToY(double wy) => oy + (int)Math.Floor((wy - vy0) * cell);

        frame.FillRect(ox - 2, oy - 2, (viewW * cell) + 4, (viewH * cell) + 4, Pixel.Pack(15, 15, 20));
        for (int y = vy0; y < vy0 + viewH; y++)
        {
            for (int x = vx0; x < vx0 + viewW; x++)
            {
                uint? color = map[x, y] switch
                {
                    Cell.Wall => Pixel.Pack(200, 200, 200),
                    Cell.Floor or Cell.Door => Pixel.Pack(50, 50, 60),
                    _ => null,
                };
                if (color is uint c)
                {
                    frame.FillRect(ToX(x), ToY(y), cell, cell, c);
                }
            }
        }

        foreach (Door d in world.Doors)
        {
            (double hx, double hy) = d.Hinge;
            (double tx, double ty) = d.Tip;
            uint doorColor = Pixel.Pack(235, 140, 40);
            frame.Line(ToX(hx), ToY(hy), ToX(tx), ToY(ty), doorColor, clip);
            frame.Line(ToX(hx) + 1, ToY(hy), ToX(tx) + 1, ToY(ty), doorColor, clip);
        }

        foreach ((int sx, int sy) in scene.Sprites)
        {
            if (sx >= vx0 && sx < vx0 + viewW && sy >= vy0 && sy < vy0 + viewH)
            {
                int s = Math.Max(2, cell / 3);
                frame.FillRect(ToX(sx + 0.5) - (s / 2), ToY(sy + 0.5) - (s / 2), s, s, Pixel.Pack(60, 200, 90));
            }
        }

        int px = ToX(player.PosX);
        int py = ToY(player.PosY);
        int n = _hits.Length;
        int stepCols = Math.Max(1, n / 24);
        uint rayColor = Pixel.Pack(230, 200, 60);
        for (int x = 0; x < n; x += stepCols)
        {
            DrawRayOnMinimap(frame, player, x, n, ToX, ToY, px, py, rayColor, clip);
        }

        DrawRayOnMinimap(frame, player, n - 1, n, ToX, ToY, px, py, rayColor, clip);
        frame.FillRect(px - 2, py - 2, 5, 5, Pixel.Pack(230, 50, 50));
    }

    private void DrawRayOnMinimap(FrameBuffer frame, Player player, int x, int n, Func<double, int> toX, Func<double, int> toY, int px, int py, uint color, Clip clip)
    {
        RayHit hit = _hits[x];
        if (!hit.Hit && double.IsInfinity(hit.Distance))
        {
            return;
        }

        (double rx, double ry) = Raycaster.RayDirection(player, Raycaster.CameraX(x, n));
        int ex = toX(player.PosX + (rx * hit.Distance));
        int ey = toY(player.PosY + (ry * hit.Distance));
        frame.Line(px, py, ex, ey, color, clip);
    }
}
