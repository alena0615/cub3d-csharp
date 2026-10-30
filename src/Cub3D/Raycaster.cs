namespace Cub3D;

public readonly record struct RayHit(
    bool Hit,
    double Distance,
    int Side,
    int MapX,
    int MapY,
    double WallX,
    Facing Facing,
    bool IsDoor = false);

public static class Raycaster
{
    public static double CameraX(int x, int width) => (2.0 * x / width) - 1.0;

    public static (double X, double Y) RayDirection(Player player, double cameraX)
    {
        ArgumentNullException.ThrowIfNull(player);
        return (player.DirX + (player.PlaneX * cameraX), player.DirY + (player.PlaneY * cameraX));
    }

    public static Facing WallFacing(int side, double rayX, double rayY) =>
        side == 0
            ? (rayX > 0 ? Facing.East : Facing.West)
            : (rayY > 0 ? Facing.South : Facing.North);

    public static double LineHeight(int screenHeight, double distance) =>
        distance <= 0 ? double.PositiveInfinity : screenHeight / distance;

    public static int TextureColumn(double wallX, Facing facing, int textureWidth)
    {
        int texX = (int)(wallX * textureWidth);
        texX = Math.Clamp(texX, 0, textureWidth - 1);
        if (facing is Facing.South or Facing.West)
        {
            texX = textureWidth - 1 - texX;
        }

        return texX;
    }

    public static RayHit CastRay(World world, double posX, double posY, double rayX, double rayY)
    {
        ArgumentNullException.ThrowIfNull(world);
        int mapX = (int)Math.Floor(posX);
        int mapY = (int)Math.Floor(posY);

        double deltaX = Math.Abs(1.0 / rayX);
        double deltaY = Math.Abs(1.0 / rayY);

        int stepX;
        double sideX;
        if (rayX < 0)
        {
            stepX = -1;
            sideX = (posX - mapX) * deltaX;
        }
        else
        {
            stepX = 1;
            sideX = (mapX + 1.0 - posX) * deltaX;
        }

        int stepY;
        double sideY;
        if (rayY < 0)
        {
            stepY = -1;
            sideY = (posY - mapY) * deltaY;
        }
        else
        {
            stepY = 1;
            sideY = (mapY + 1.0 - posY) * deltaY;
        }

        int maxSteps = 2 * (world.Map.Width + world.Map.Height);
        int side = 0;
        bool hit = false;
        for (int steps = 0; steps < maxSteps; steps++)
        {
            if (sideX < sideY)
            {
                sideX += deltaX;
                mapX += stepX;
                side = 0;
            }
            else
            {
                sideY += deltaY;
                mapY += stepY;
                side = 1;
            }

            if (world.Map[mapX, mapY] is Cell.Wall or Cell.Void)
            {
                hit = true;
                break;
            }
        }

        RayHit wall;
        if (!hit)
        {
            wall = new RayHit(false, double.PositiveInfinity, side, mapX, mapY, 0, WallFacing(side, rayX, rayY));
        }
        else
        {
            double dist = side == 0 ? sideX - deltaX : sideY - deltaY;
            double hitCoord = side == 0 ? posY + (dist * rayY) : posX + (dist * rayX);
            double wallX = hitCoord - Math.Floor(hitCoord);
            wall = new RayHit(true, dist, side, mapX, mapY, wallX, WallFacing(side, rayX, rayY));
        }

        RayHit best = wall;
        foreach (Door d in world.Doors)
        {
            double limit = best.IsDoor ? best.Distance : best.Distance + 1e-6;
            if (TryHitLeaf(d, posX, posY, rayX, rayY, out RayHit leaf) && leaf.Distance < limit)
            {
                best = leaf;
            }
        }

        return best;
    }

    private static bool TryHitLeaf(Door door, double posX, double posY, double rayX, double rayY, out RayHit hit)
    {
        hit = default;
        (double hx, double hy) = door.Hinge;
        (double tx, double ty) = door.Tip;
        double dx = tx - hx, dy = ty - hy;
        double denom = (rayX * dy) - (rayY * dx);
        if (Math.Abs(denom) < 1e-12)
        {
            return false;
        }

        double wx = hx - posX, wy = hy - posY;
        double t = ((wx * dy) - (wy * dx)) / denom;
        double u = ((wx * rayY) - (wy * rayX)) / denom;
        if (t <= 1e-6 || u < 0 || u > 1)
        {
            return false;
        }

        int side = Math.Abs(dx) >= Math.Abs(dy) ? 1 : 0;
        hit = new RayHit(true, t, side, door.X, door.Y, u, WallFacing(side, rayX, rayY), IsDoor: true);
        return true;
    }

    public static RayHit CastColumn(World world, Player player, int x, int width)
    {
        ArgumentNullException.ThrowIfNull(player);
        (double rx, double ry) = RayDirection(player, CameraX(x, width));
        return CastRay(world, player.PosX, player.PosY, rx, ry);
    }
}
