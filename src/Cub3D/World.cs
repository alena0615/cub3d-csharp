namespace Cub3D;

public sealed class Door
{
    public Door(int x, int y, bool alongX)
    {
        X = x;
        Y = y;
        AlongX = alongX;
    }

    public int X { get; }

    public int Y { get; }

    public bool AlongX { get; }

    public double Openness { get; internal set; }

    public bool Opening { get; internal set; }

    public int Swing { get; internal set; } = 1;

    public (double X, double Y) Hinge => AlongX ? (X, Y + 0.5) : (X + 0.5, Y);

    public (double X, double Y) ClosedDir => AlongX ? (1.0, 0.0) : (0.0, 1.0);

    public (double X, double Y) Normal => AlongX ? (0.0, 1.0) : (1.0, 0.0);

    public (double X, double Y) Tip
    {
        get
        {
            double a = Openness * Math.PI / 2;
            (double hx, double hy) = Hinge;
            (double cx, double cy) = ClosedDir;
            (double nx, double ny) = Normal;
            double c = Math.Cos(a), s = Math.Sin(a) * Swing;
            return (hx + (cx * c) + (nx * s), hy + (cy * c) + (ny * s));
        }
    }
}

public sealed class World
{
    public const double DoorSpeed = 1.5;

    public const double DoorPassable = 0.9;

    public const double DoorReach = 1.5;

    public const double LeafClearance = 0.15;

    private readonly Door?[] _doorAt;
    private readonly List<(double X, double Y, double R)> _obstacles = [];

    public World(GameMap map)
    {
        ArgumentNullException.ThrowIfNull(map);
        Map = map;
        _doorAt = new Door?[map.Width * map.Height];
        var doors = new List<Door>();
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                if (map[x, y] == Cell.Door)
                {
                    var d = new Door(x, y, map[x - 1, y] == Cell.Wall && map[x + 1, y] == Cell.Wall);
                    doors.Add(d);
                    _doorAt[(y * map.Width) + x] = d;
                }
            }
        }

        Doors = doors;
    }

    public GameMap Map { get; }

    public IReadOnlyList<Door> Doors { get; }

    public Door? DoorAt(int x, int y) =>
        x >= 0 && y >= 0 && x < Map.Width && y < Map.Height ? _doorAt[(y * Map.Width) + x] : null;

    public bool IsBlocking(int x, int y)
    {
        Cell c = Map[x, y];
        return c switch
        {
            Cell.Wall or Cell.Void => true,
            Cell.Door => DoorOpenness(x, y) < DoorPassable,
            _ => false,
        };
    }

    public bool IsWalkable(int x, int y) => !IsBlocking(x, y);

    public void AddObstacle(double x, double y, double radius) => _obstacles.Add((x, y, radius));

    public bool IsFree(double x, double y)
    {
        if (IsBlocking((int)Math.Floor(x), (int)Math.Floor(y)))
        {
            return false;
        }

        foreach (Door d in Doors)
        {
            if (d.Openness > 0 && DistanceToSegment(x, y, d.Hinge, d.Tip) < LeafClearance)
            {
                return false;
            }
        }

        foreach ((double ox, double oy, double r) in _obstacles)
        {
            if (((x - ox) * (x - ox)) + ((y - oy) * (y - oy)) < r * r)
            {
                return false;
            }
        }

        return true;
    }

    public double DoorOpenness(int x, int y) => DoorAt(x, y)?.Openness ?? 0;

    public bool IsDoorOpen(int x, int y) => DoorOpenness(x, y) >= 1;

    public bool IsDoorOpening(int x, int y) => DoorAt(x, y)?.Opening ?? false;

    public bool IsDoorAlongX(int x, int y) => DoorAt(x, y)?.AlongX ?? false;

    public void Update(double dt)
    {
        foreach (Door d in Doors)
        {
            double target = d.Opening ? 1 : 0;
            d.Openness = d.Openness < target
                ? Math.Min(target, d.Openness + (DoorSpeed * dt))
                : Math.Max(target, d.Openness - (DoorSpeed * dt));
        }
    }

    public bool ToggleDoor(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        Door? door = FindDoorInFront(player);
        if (door is null)
        {
            return false;
        }

        (double hx, double hy) = door.Hinge;
        (double nx, double ny) = door.Normal;
        (double cx, double cy) = door.ClosedDir;
        double vx = player.PosX - hx, vy = player.PosY - hy;

        if (!door.Opening)
        {
            if (door.Openness == 0)
            {
                door.Swing = ((vx * nx) + (vy * ny)) > 0 ? -1 : 1;
            }

            door.Opening = true;
            return true;
        }

        double m = Player.CollisionMargin;
        bool inCell = player.PosX + m > door.X && player.PosX - m < door.X + 1
            && player.PosY + m > door.Y && player.PosY - m < door.Y + 1;
        double along = (vx * cx) + (vy * cy);
        double across = ((vx * nx) + (vy * ny)) * door.Swing;
        bool inSweep = across > -m && along > -m && Math.Sqrt((vx * vx) + (vy * vy)) < 1 + (m / 2);
        if (inCell || inSweep)
        {
            return false;
        }

        door.Opening = false;
        return true;
    }

    private Door? FindDoorInFront(Player player)
    {
        for (double reach = 0.25; reach <= DoorReach + 1e-9; reach += 0.25)
        {
            int cx = (int)Math.Floor(player.PosX + (player.DirX * reach));
            int cy = (int)Math.Floor(player.PosY + (player.DirY * reach));
            if (DoorAt(cx, cy) is Door d)
            {
                return d;
            }

            if (Map[cx, cy] is Cell.Wall or Cell.Void)
            {
                return null;
            }
        }

        return null;
    }

    private static double DistanceToSegment(double px, double py, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double len2 = (dx * dx) + (dy * dy);
        double t = len2 == 0 ? 0 : Math.Clamp((((px - a.X) * dx) + ((py - a.Y) * dy)) / len2, 0, 1);
        double ex = a.X + (t * dx) - px, ey = a.Y + (t * dy) - py;
        return Math.Sqrt((ex * ex) + (ey * ey));
    }
}
