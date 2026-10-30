namespace Cub3D.Tests;

public class RaycastTests
{
    private const int P = 9;

    private static readonly World Room = new(GameMap.FromRows(
        "11111",
        "10001",
        "10001",
        "10001",
        "11111"));

    [Theory]
    [InlineData(1.0, 0.0, 0, Facing.East, 4, 2)]
    [InlineData(-1.0, 0.0, 0, Facing.West, 0, 2)]
    [InlineData(0.0, 1.0, 1, Facing.South, 2, 4)]
    [InlineData(0.0, -1.0, 1, Facing.North, 2, 0)]
    public void AxisRays_FromRoomCenter(double rx, double ry, int side, Facing facing, int mapX, int mapY)
    {
        RayHit h = Raycaster.CastRay(Room, 2.5, 2.5, rx, ry);

        Assert.True(h.Hit);
        Assert.Equal(1.5, h.Distance, P);
        Assert.Equal(side, h.Side);
        Assert.Equal(facing, h.Facing);
        Assert.Equal(mapX, h.MapX);
        Assert.Equal(mapY, h.MapY);
        Assert.Equal(0.5, h.WallX, P);
    }

    [Fact]
    public void AngledRay_PerpendicularDistanceAndWallX()
    {
        RayHit h = Raycaster.CastRay(Room, 2.5, 2.5, 1, 0.5);

        Assert.Equal(1.5, h.Distance, P);
        Assert.Equal(0, h.Side);
        Assert.Equal(Facing.East, h.Facing);
        Assert.Equal(4, h.MapX);
        Assert.Equal(3, h.MapY);
        Assert.Equal(0.25, h.WallX, P);
    }

    [Fact]
    public void Dda_StepsThroughCellBoundaries_LikeFigure4()
    {
        var world = new World(GameMap.FromRows(
            "00001",
            "00000",
            "00000"));
        RayHit h = Raycaster.CastRay(world, 0.7, 2.6, 1, -0.5);

        Assert.Equal(4, h.MapX);
        Assert.Equal(0, h.MapY);
        Assert.Equal(0, h.Side);
        Assert.Equal(3.3, h.Distance, P);
    }

    [Fact]
    public void StraightWall_HasSameDistanceInAllColumns_NoFishEye()
    {
        var world = new World(GameMap.FromRows(
            "1111111111111",
            "1000000000001",
            "1000000000001",
            "1000000000001",
            "1000000000001",
            "1111111111111"));
        var p = new Player(6.5, 3.5, 0, -1);

        for (int x = 0; x < 960; x += 37)
        {
            RayHit h = Raycaster.CastColumn(world, p, x, 960);
            Assert.Equal(2.5, h.Distance, P);
        }
    }

    [Fact]
    public void CameraX_RunsFromMinusOneToAlmostOne()
    {
        Assert.Equal(-1, Raycaster.CameraX(0, 960), P);
        Assert.Equal(0, Raycaster.CameraX(480, 960), P);
        Assert.True(Raycaster.CameraX(959, 960) < 1);
    }

    [Fact]
    public void RayDirection_IsDirPlusPlaneTimesCamX()
    {
        var p = Player.FromStart(0, 0, 'N');
        (double x, double y) = Raycaster.RayDirection(p, 1);

        Assert.Equal(Player.PlaneLength, x, P);
        Assert.Equal(-1, y, P);
    }

    [Fact]
    public void LineHeight_IsInverseToDistance()
    {
        Assert.Equal(600, Raycaster.LineHeight(600, 1), P);
        Assert.Equal(300, Raycaster.LineHeight(600, 2), P);
        Assert.Equal(1200, Raycaster.LineHeight(600, 0.5), P);
    }

    [Theory]
    [InlineData(Facing.North, 16)]
    [InlineData(Facing.East, 16)]
    [InlineData(Facing.South, 47)]
    [InlineData(Facing.West, 47)]
    public void TextureColumn_IsMirroredForSouthAndWest(Facing facing, int expected)
    {
        Assert.Equal(expected, Raycaster.TextureColumn(0.25, facing, 64));
    }

    [Fact]
    public void TextureColumn_StaysInRange()
    {
        Assert.Equal(63, Raycaster.TextureColumn(0.999999, Facing.North, 64));
        Assert.Equal(0, Raycaster.TextureColumn(0.999999, Facing.South, 64));
    }

    [Fact]
    public void OpenMap_DoesNotHang()
    {
        var world = new World(GameMap.FromRows("000", "000"));
        RayHit h = Raycaster.CastRay(world, 1.5, 0.5, 1, 0.0001);

        Assert.True(h.Distance < 100);
    }

    private static World DoorCorridor() => new(GameMap.FromRows(
        "1111111",
        "10D0001",
        "1111111"));

    [Fact]
    public void ClosedDoor_IsHitInTheMiddleOfItsCell()
    {
        World world = DoorCorridor();
        RayHit h = Raycaster.CastRay(world, 1.5, 1.5, 1, 0);

        Assert.Equal(1.0, h.Distance, P);
        Assert.True(h.IsDoor);
        Assert.Equal(2, h.MapX);
        Assert.Equal(0.5, h.WallX, P);
        Assert.False(world.IsWalkable(2, 1));
    }

    [Fact]
    public void Door_OpensGraduallyAndLetsPlayerThrough()
    {
        World world = DoorCorridor();
        var p = new Player(1.5, 1.5, 1, 0);

        Assert.True(world.ToggleDoor(p));
        Assert.False(world.IsDoorOpen(2, 1));
        world.Update(0.2);
        Assert.Equal(0.2 * World.DoorSpeed, world.DoorOpenness(2, 1), P);
        Assert.False(world.IsWalkable(2, 1));

        world.Update(1.0);
        Assert.True(world.IsDoorOpen(2, 1));
        Assert.True(world.IsWalkable(2, 1));
        Assert.Equal(4.5, Raycaster.CastRay(world, 1.5, 1.5, 1, 0).Distance, P);
    }

    [Fact]
    public void HalfOpenDoor_IsRotatedLeaf()
    {
        World world = DoorCorridor();
        world.ToggleDoor(new Player(1.5, 1.5, 1, 0));
        world.Update(0.5 / World.DoorSpeed);

        RayHit h = Raycaster.CastRay(world, 1.5, 1.5, 1, 0);
        Assert.True(h.IsDoor);
        Assert.Equal(1.5, h.Distance, P);
        Assert.Equal(Math.Sqrt(0.5), h.WallX, P);

        Assert.False(Raycaster.CastRay(world, 1.5, 1.9, 1, 0).IsDoor);
    }

    [Fact]
    public void OpenDoor_StaysVisibleAlongTheWall()
    {
        World world = DoorCorridor();
        world.ToggleDoor(new Player(1.5, 1.5, 1, 0));
        world.Update(1.0);

        RayHit h = Raycaster.CastRay(world, 3.0, 1.5, 0, -1);
        Assert.True(h.IsDoor);
        Assert.Equal(0.5, h.Distance, P);
    }

    [Fact]
    public void Door_SwingsAwayFromPlayer()
    {
        World fromWest = DoorCorridor();
        fromWest.ToggleDoor(new Player(1.5, 1.5, 1, 0));
        Assert.Equal(1, fromWest.Doors[0].Swing);

        World fromEast = DoorCorridor();
        fromEast.ToggleDoor(new Player(3.5, 1.5, -1, 0));
        Assert.Equal(-1, fromEast.Doors[0].Swing);
    }

    [Fact]
    public void PlayerCannotWalkThroughOpenLeaf()
    {
        World world = DoorCorridor();
        world.ToggleDoor(new Player(1.5, 1.5, 1, 0));
        world.Update(0.5 / World.DoorSpeed);

        Assert.False(world.IsFree(3.0, 1.5));
        Assert.True(world.IsFree(1.5, 1.5));
    }

    [Fact]
    public void Door_ClosesAgain()
    {
        World world = DoorCorridor();
        var p = new Player(1.5, 1.5, 1, 0);
        world.ToggleDoor(p);
        world.Update(1.0);

        Assert.True(world.ToggleDoor(p));
        world.Update(0.3);
        Assert.False(world.IsDoorOpen(2, 1));
        Assert.True(world.DoorOpenness(2, 1) > 0);
        world.Update(1.0);
        Assert.Equal(0, world.DoorOpenness(2, 1), P);
        Assert.Equal(1.0, Raycaster.CastRay(world, 1.5, 1.5, 1, 0).Distance, P);
    }

    [Fact]
    public void Door_DoesNotCloseOnPlayerInDoorway()
    {
        var world = new World(GameMap.FromRows(
            "11111",
            "10D01",
            "11111"));
        var outside = new Player(1.5, 1.5, 1, 0);
        world.ToggleDoor(outside);
        world.Update(1.0);

        var inDoorway = new Player(1.9, 1.5, 1, 0);
        Assert.False(world.ToggleDoor(inDoorway));
        Assert.True(world.IsDoorOpening(2, 1));

        Assert.True(world.ToggleDoor(outside));
        Assert.False(world.IsDoorOpening(2, 1));
    }

    [Fact]
    public void DoorAlongX_IsHitAtHalfCellInY()
    {
        var world = new World(GameMap.FromRows(
            "111",
            "101",
            "1D1",
            "101",
            "111"));

        Assert.True(world.IsDoorAlongX(1, 2));
        RayHit h = Raycaster.CastRay(world, 1.5, 1.5, 0, 1);
        Assert.Equal(1.0, h.Distance, P);
        Assert.Equal(1, h.Side);
    }
}
