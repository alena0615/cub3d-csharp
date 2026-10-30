namespace Cub3D;

public readonly record struct Ellipsoid(double X, double Y, double Z, double Rx, double Ry, double Rz, uint Color);

public sealed class Model3D
{
    public Model3D(IReadOnlyList<Ellipsoid> parts, double scale)
    {
        ArgumentNullException.ThrowIfNull(parts);
        Parts = parts;
        Scale = scale;
        BoundingRadius = parts.Max(p => Math.Sqrt((p.X * p.X) + (p.Z * p.Z)) + Math.Max(p.Rx, p.Rz)) * scale;
        Height = parts.Max(p => p.Y + p.Ry) * scale;
    }

    public IReadOnlyList<Ellipsoid> Parts { get; }

    public double Scale { get; }

    public double BoundingRadius { get; }

    public double Height { get; }

    public static Model3D Chicken()
    {
        uint white = Pixel.Pack(250, 248, 240);
        uint wing = Pixel.Pack(232, 226, 214);
        uint red = Pixel.Pack(220, 40, 40);
        uint orange = Pixel.Pack(240, 150, 30);
        uint black = Pixel.Pack(25, 25, 25);
        Ellipsoid[] parts =
        [
            new(0.00, 0.42, 0.00, 0.36, 0.27, 0.27, white),
            new(-0.32, 0.60, 0.00, 0.13, 0.21, 0.09, white),
            new(-0.03, 0.45, 0.24, 0.25, 0.14, 0.06, wing),
            new(-0.03, 0.45, -0.24, 0.25, 0.14, 0.06, wing),
            new(0.26, 0.78, 0.00, 0.16, 0.17, 0.15, white),
            new(0.19, 0.97, 0.00, 0.055, 0.055, 0.04, red),
            new(0.27, 1.00, 0.00, 0.065, 0.065, 0.045, red),
            new(0.35, 0.96, 0.00, 0.05, 0.05, 0.04, red),
            new(0.43, 0.78, 0.00, 0.09, 0.035, 0.045, orange),
            new(0.37, 0.67, 0.00, 0.035, 0.065, 0.03, red),
            new(0.34, 0.83, 0.11, 0.03, 0.03, 0.03, black),
            new(0.34, 0.83, -0.11, 0.03, 0.03, 0.03, black),
            new(0.04, 0.11, 0.09, 0.025, 0.12, 0.025, orange),
            new(0.04, 0.11, -0.09, 0.025, 0.12, 0.025, orange),
            new(0.10, 0.015, 0.09, 0.10, 0.018, 0.045, orange),
            new(0.10, 0.015, -0.09, 0.10, 0.018, 0.045, orange),
        ];
        return new Model3D(parts, 0.5);
    }
}

public sealed class PlacedModel
{
    public PlacedModel(Model3D model, double x, double y, double facingX, double facingY)
    {
        ArgumentNullException.ThrowIfNull(model);
        double len = Math.Sqrt((facingX * facingX) + (facingY * facingY));
        Model = model;
        X = x;
        Y = y;
        ForwardX = len < 1e-9 ? 0 : facingX / len;
        ForwardY = len < 1e-9 ? 1 : facingY / len;
    }

    public Model3D Model { get; }

    public double X { get; }

    public double Y { get; }

    public double ForwardX { get; }

    public double ForwardY { get; }

    public double SideX => -ForwardY;

    public double SideY => ForwardX;

    public (double X, double Y, double H) ToWorld(double mx, double my, double mz)
    {
        double s = Model.Scale;
        return (X + (s * ((ForwardX * mx) + (SideX * mz))), Y + (s * ((ForwardY * mx) + (SideY * mz))), s * my);
    }
}

public static class EasterEgg
{
    public static (int X, int Y)? FindCorner(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        GameMap map = scene.Map;
        var taken = new HashSet<(int, int)>(scene.Sprites) { (scene.StartX, scene.StartY) };
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = map.Width - 1; x >= 0; x--)
            {
                if (map[x, y] == Cell.Floor && !taken.Contains((x, y)))
                {
                    return (x, y);
                }
            }
        }

        return null;
    }

    public static PlacedModel? PlaceChicken(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        if (FindCorner(scene) is not (int x, int y))
        {
            return null;
        }

        double cx = x + 0.5, cy = y + 0.5;
        return new PlacedModel(Model3D.Chicken(), cx, cy, (scene.Map.Width / 2.0) - cx, (scene.Map.Height / 2.0) - cy);
    }
}
