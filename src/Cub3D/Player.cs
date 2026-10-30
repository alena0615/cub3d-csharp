namespace Cub3D;

public sealed class Player
{
    public const double FovDegrees = 66.0;

    public const double CollisionMargin = 0.2;

    public static readonly double PlaneLength = Math.Tan(FovDegrees * Math.PI / 360.0);

    public Player(double posX, double posY, double dirX, double dirY)
    {
        PosX = posX;
        PosY = posY;
        DirX = dirX;
        DirY = dirY;

        PlaneX = -dirY * PlaneLength;
        PlaneY = dirX * PlaneLength;
    }

    public double PosX { get; private set; }

    public double PosY { get; private set; }

    public double DirX { get; private set; }

    public double DirY { get; private set; }

    public double PlaneX { get; private set; }

    public double PlaneY { get; private set; }

    public static Player FromStart(int cellX, int cellY, char direction)
    {
        (double dx, double dy) = direction switch
        {
            'N' => (0.0, -1.0),
            'S' => (0.0, 1.0),
            'E' => (1.0, 0.0),
            'W' => (-1.0, 0.0),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "ожидается N, S, E или W"),
        };
        return new Player(cellX + 0.5, cellY + 0.5, dx, dy);
    }

    public static Player FromScene(Scene scene)
    {
        ArgumentNullException.ThrowIfNull(scene);
        return FromStart(scene.StartX, scene.StartY, scene.StartDirection);
    }

    public void Rotate(double angle)
    {
        double c = Math.Cos(angle);
        double s = Math.Sin(angle);
        (DirX, DirY) = ((DirX * c) - (DirY * s), (DirX * s) + (DirY * c));
        (PlaneX, PlaneY) = ((PlaneX * c) - (PlaneY * s), (PlaneX * s) + (PlaneY * c));
    }

    public void Move(double dx, double dy, Func<int, int, bool> isWalkable, Func<double, double, bool>? isFree = null)
    {
        ArgumentNullException.ThrowIfNull(isWalkable);
        if (dx != 0)
        {
            double probeX = PosX + dx + (Math.Sign(dx) * CollisionMargin);
            if (isWalkable((int)Math.Floor(probeX), (int)Math.Floor(PosY)) && (isFree is null || isFree(PosX + dx, PosY)))
            {
                PosX += dx;
            }
        }

        if (dy != 0)
        {
            double probeY = PosY + dy + (Math.Sign(dy) * CollisionMargin);
            if (isWalkable((int)Math.Floor(PosX), (int)Math.Floor(probeY)) && (isFree is null || isFree(PosX, PosY + dy)))
            {
                PosY += dy;
            }
        }
    }

    public void Walk(double forward, double strafe, Func<int, int, bool> isWalkable, Func<double, double, bool>? isFree = null)
    {
        double dx = (DirX * forward) + (-DirY * strafe);
        double dy = (DirY * forward) + (DirX * strafe);
        Move(dx, dy, isWalkable, isFree);
    }
}
