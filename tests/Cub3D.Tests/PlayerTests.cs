namespace Cub3D.Tests;

public class PlayerTests
{
    private const int P = 9;

    [Theory]
    [InlineData('N', 0.0, -1.0, 1.0, 0.0)]
    [InlineData('S', 0.0, 1.0, -1.0, 0.0)]
    [InlineData('E', 1.0, 0.0, 0.0, 1.0)]
    [InlineData('W', -1.0, 0.0, 0.0, -1.0)]
    public void StartVectors_ForFourDirections(char d, double dx, double dy, double px, double py)
    {
        var p = Player.FromStart(3, 4, d);

        Assert.Equal(3.5, p.PosX, P);
        Assert.Equal(4.5, p.PosY, P);
        Assert.Equal(dx, p.DirX, P);
        Assert.Equal(dy, p.DirY, P);
        Assert.Equal(px * Player.PlaneLength, p.PlaneX, P);
        Assert.Equal(py * Player.PlaneLength, p.PlaneY, P);
    }

    [Fact]
    public void Plane_IsPerpendicularAndGivesFov66()
    {
        var p = Player.FromStart(0, 0, 'N');
        p.Rotate(0.7);

        Assert.Equal(0, (p.DirX * p.PlaneX) + (p.DirY * p.PlaneY), P);
        Assert.Equal(0.6494, Player.PlaneLength, 4);
        double planeLen = Math.Sqrt((p.PlaneX * p.PlaneX) + (p.PlaneY * p.PlaneY));
        Assert.Equal(66.0, 2 * Math.Atan(planeLen) * 180 / Math.PI, 6);
    }

    [Fact]
    public void Rotate90Right_FromNorthLooksEast()
    {
        var p = Player.FromStart(0, 0, 'N');
        p.Rotate(Math.PI / 2);

        Assert.Equal(1, p.DirX, P);
        Assert.Equal(0, p.DirY, P);
    }

    [Fact]
    public void Rotate360_ReturnsToStartAndKeepsLength()
    {
        var p = Player.FromStart(0, 0, 'E');
        for (int i = 0; i < 36; i++)
        {
            p.Rotate(Math.PI / 18);
        }

        Assert.Equal(1, p.DirX, P);
        Assert.Equal(0, p.DirY, P);
        Assert.Equal(1, Math.Sqrt((p.DirX * p.DirX) + (p.DirY * p.DirY)), P);
    }

    private static readonly World Room = new(GameMap.FromRows(
        "1111111",
        "1000001",
        "1000001",
        "1000001",
        "1000001",
        "1111111"));

    [Fact]
    public void WalkForwardAndStrafe()
    {
        var p = Player.FromStart(3, 3, 'N');
        p.Walk(1.0, 0, Room.IsWalkable);
        Assert.Equal(3.5, p.PosX, P);
        Assert.Equal(2.5, p.PosY, P);

        p.Walk(0, 1.0, Room.IsWalkable);
        Assert.Equal(4.5, p.PosX, P);
        Assert.Equal(2.5, p.PosY, P);
    }

    [Fact]
    public void Wall_BlocksMovementWithMargin()
    {
        var p = Player.FromStart(1, 1, 'N');
        p.Walk(0.4, 0, Room.IsWalkable);
        Assert.Equal(1.5, p.PosY, P);

        p.Walk(0.25, 0, Room.IsWalkable);
        Assert.Equal(1.25, p.PosY, P);
    }

    [Fact]
    public void DiagonalIntoWall_SlidesAlongIt()
    {
        var p = Player.FromStart(2, 1, 'N');
        p.Move(0.3, -0.4, Room.IsWalkable);

        Assert.Equal(2.8, p.PosX, P);
        Assert.Equal(1.5, p.PosY, P);
    }

    [Fact]
    public void Game_SpeedDependsOnTimeNotFrameCount()
    {
        Scene scene = TestData.SceneFromRows(3, 8, 'N', Rows(12));
        var slow = new Game(scene, TestData.SolidTextures(), 32, 32);
        var fast = new Game(scene, TestData.SolidTextures(), 32, 32);
        var forward = new InputState(true, false, false, false, false, false);

        for (int i = 0; i < 30; i++)
        {
            slow.Update(forward, 1.0 / 30);
        }

        for (int i = 0; i < 144; i++)
        {
            fast.Update(forward, 1.0 / 144);
        }

        Assert.Equal(slow.Player.PosY, fast.Player.PosY, 6);
        Assert.Equal(8.5 - Game.MoveSpeed, slow.Player.PosY, 6);
    }

    [Fact]
    public void Shift_RunsFaster()
    {
        Scene scene = TestData.SceneFromRows(3, 8, 'N', Rows(12));
        var walk = new Game(scene, TestData.SolidTextures(), 32, 32);
        var run = new Game(scene, TestData.SolidTextures(), 32, 32);

        walk.Update(new InputState(true, false, false, false, false, false), 0.5);
        run.Update(new InputState(true, false, false, false, false, false, Run: true), 0.5);

        double walked = 8.5 - walk.Player.PosY;
        double ran = 8.5 - run.Player.PosY;
        Assert.Equal(Game.RunMultiplier * walked, ran, 6);
    }

    private static string[] Rows(int h)
    {
        var rows = new string[h];
        rows[0] = rows[h - 1] = "1111111";
        for (int i = 1; i < h - 1; i++)
        {
            rows[i] = "1000001";
        }

        return rows;
    }
}
