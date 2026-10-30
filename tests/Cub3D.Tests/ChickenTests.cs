namespace Cub3D.Tests;

public class ChickenTests
{
    [Theory]
    [InlineData("simple.cub", 8, 1)]
    [InlineData("42.cub", 20, 1)]
    [InlineData("irregular.cub", 13, 1)]
    [InlineData("maze.cub", 29, 1)]
    [InlineData("bonus.cub", 19, 1)]
    public void Chicken_StandsInTopRightCornerOfEveryMap(string name, int x, int y)
    {
        Scene s = SceneParser.ParseFile(TestData.MapPath(name));
        var game = new Game(s, TextureSet.Load(s), 64, 40);

        Assert.NotNull(game.Chicken);
        Assert.Equal(x + 0.5, game.Chicken.X, 9);
        Assert.Equal(y + 0.5, game.Chicken.Y, 9);
    }

    [Fact]
    public void Chicken_DoesNotTurnTowardsPlayer()
    {
        Scene s = SceneParser.ParseFile(TestData.MapPath("bonus.cub"));
        var game = new Game(s, TextureSet.Load(s), 64, 40);
        PlacedModel c = game.Chicken!;
        (double fx, double fy) = (c.ForwardX, c.ForwardY);

        game.Update(new InputState(true, false, false, true, false, true, MouseDeltaX: 40), 0.1);
        game.Render();

        Assert.Equal(fx, game.Chicken!.ForwardX, 12);
        Assert.Equal(fy, game.Chicken.ForwardY, 12);
    }

    [Fact]
    public void Chicken_IsSolid3DObject_EyesVisibleFromFrontNotFromBehind()
    {
        int front = DarkPixels(new Player(4.5, 6.5, 0, -1));
        int behind = DarkPixels(new Player(4.5, 2.5, 0, 1));

        Assert.True(front > 0);
        Assert.Equal(0, behind);
    }

    [Fact]
    public void Chicken_SideViewIsWiderThanFrontView()
    {
        int front = ChickenWidth(new Player(4.5, 6.5, 0, -1));
        int side = ChickenWidth(new Player(2.5, 4.5, 1, 0));

        Assert.True(side > front * 1.2, $"спереди {front}, сбоку {side}");
    }

    [Fact]
    public void Chicken_IsHiddenBehindWall()
    {
        Scene s = Room("111111111", "100000001", "100000001", "100000001", "111111111", "100000001", "100000001", "100000001", "111111111");
        var chicken = new PlacedModel(Model3D.Chicken(), 4.5, 6.5, 0, 1);
        FrameBuffer f = RenderWith(s, new Player(4.5, 2.5, 0, 1), chicken);

        Assert.Equal(0, CountChicken(f));
    }

    [Fact]
    public void Player_CannotWalkThroughChicken()
    {
        Scene s = SceneParser.ParseFile(TestData.MapPath("simple.cub"));
        var game = new Game(s, TextureSet.Load(s), 64, 40);
        PlacedModel c = game.Chicken!;

        Assert.False(game.World.IsFree(c.X, c.Y));
        Assert.False(game.World.IsFree(c.X + 0.3, c.Y));
        Assert.True(game.World.IsFree(c.X - 0.5, c.Y + 0.5));
    }

    private static Scene Room(params string[] rows) => TestData.SceneFromRows(1, 1, 'N', rows);

    private static Scene OpenRoom() => Room(
        "111111111",
        "100000001",
        "100000001",
        "100000001",
        "100000001",
        "100000001",
        "100000001",
        "100000001",
        "111111111");

    private static FrameBuffer RenderWith(Scene scene, Player player, PlacedModel model)
    {
        var frame = new FrameBuffer(320, 200);
        new Renderer().Render(new World(scene.Map), player, scene, TestData.SolidTextures(), frame, new RenderOptions(), [model]);
        return frame;
    }

    private static FrameBuffer RenderChicken(Player player) =>
        RenderWith(OpenRoom(), player, new PlacedModel(Model3D.Chicken(), 4.5, 4.5, 0, 1));

    private static int DarkPixels(Player player)
    {
        FrameBuffer f = RenderChicken(player);

        return f.Pixels.Count(p => Pixel.R(p) < 30 && Pixel.R(p) == Pixel.G(p) && Pixel.G(p) == Pixel.B(p));
    }

    private static int ChickenWidth(Player player)
    {
        FrameBuffer f = RenderChicken(player);
        int min = int.MaxValue, max = -1;
        for (int y = 0; y < f.Height; y++)
        {
            for (int x = 0; x < f.Width; x++)
            {
                if (IsChickenWhite(f[x, y]))
                {
                    min = Math.Min(min, x);
                    max = Math.Max(max, x);
                }
            }
        }

        return max - min;
    }

    private static int CountChicken(FrameBuffer f) => f.Pixels.Count(IsChickenWhite);

    private static bool IsChickenWhite(uint p) =>
        Pixel.R(p) > 90 && Math.Abs(Pixel.R(p) - Pixel.B(p)) < 20 && Math.Abs(Pixel.R(p) - Pixel.G(p)) < 20;
}
