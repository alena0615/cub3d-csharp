namespace Cub3D.Tests;

public class RenderTests
{
    private static FrameBuffer RenderScene(Scene scene, int w, int h, TextureSet? textures = null, RenderOptions? options = null)
    {
        var game = new Game(scene, textures ?? TestData.SolidTextures(), w, h);
        if (options is not null)
        {
            game.Options.Minimap = options.Minimap;
            game.Options.Shading = options.Shading;
        }

        game.Render();
        return game.Frame;
    }

    private static Scene NorthRoom() => TestData.SceneFromRows(
        2,
        2,
        'N',
        "11111",
        "10001",
        "10001",
        "10001",
        "10001",
        "11111");

    [Fact]
    public void CeilingTopFloorBottom()
    {
        Scene s = TestData.SceneFromRows(10, 10, 'N', BigRoom(21));
        FrameBuffer f = RenderScene(s, 64, 40);

        Assert.Equal(s.Ceiling.Packed, f[0, 0]);
        Assert.Equal(s.Ceiling.Packed, f[63, 5]);
        Assert.Equal(s.Floor.Packed, f[0, 39]);
        Assert.Equal(s.Floor.Packed, f[63, 34]);
    }

    [Fact]
    public void WallStripOfKnownHeightInCenter()
    {
        Scene s = NorthRoom();
        FrameBuffer f = RenderScene(s, 40, 90);
        int cx = 20;

        Assert.Equal(s.Ceiling.Packed, f[cx, 14]);
        Assert.Equal(TestData.NorthColor, f[cx, 15]);
        Assert.Equal(TestData.NorthColor, f[cx, 45]);
        Assert.Equal(TestData.NorthColor, f[cx, 74]);
        Assert.Equal(s.Floor.Packed, f[cx, 75]);
    }

    [Fact]
    public void WallCloserThanOneCell_FillsWholeColumn()
    {
        var game = new Game(NorthRoom(), TestData.SolidTextures(), 40, 60);
        game.Player.Walk(1.0, 0, game.World.IsWalkable);

        game.Render();
        for (int y = 0; y < 60; y++)
        {
            Assert.Equal(TestData.NorthColor, game.Frame[20, y]);
        }
    }

    [Fact]
    public void EachSideUsesItsTexture()
    {
        Scene s = NorthRoom();
        var game = new Game(s, TestData.SolidTextures(), 40, 60);
        int mid = 30;

        uint Center()
        {
            game.Render();
            return game.Frame[20, mid];
        }

        Assert.Equal(TestData.NorthColor, Center());
        game.Player.Rotate(Math.PI / 2);
        Assert.Equal(TestData.EastColor, Center());
        game.Player.Rotate(Math.PI / 2);
        Assert.Equal(TestData.SouthColor, Center());
        game.Player.Rotate(Math.PI / 2);
        Assert.Equal(TestData.WestColor, Center());
    }

    [Fact]
    public void Shading_DarkensFarWalls()
    {
        Assert.Equal(1.0, Renderer.ShadeFactor(0, 0), 9);
        Assert.Equal(0.8, Renderer.ShadeFactor(0, 1), 9);
        Assert.True(Renderer.ShadeFactor(10, 0) < Renderer.ShadeFactor(2, 0));

        FrameBuffer plain = RenderScene(NorthRoom(), 40, 60);
        FrameBuffer shaded = RenderScene(NorthRoom(), 40, 60, options: new RenderOptions { Shading = true });
        Assert.True(Pixel.R(shaded[20, 30]) < Pixel.R(plain[20, 30]));
    }

    [Fact]
    public void Sprite_IsDrawnInFrontOfWallAndHiddenBehindIt()
    {
        uint green = Pixel.Pack(0, 200, 0);
        Texture spriteTex = Texture.Solid(64, 64, green);
        Scene s = TestData.SceneFromRows(
            3,
            5,
            'N',
            "1111111",
            "1000001",
            "1000001",
            "1000001",
            "1000001",
            "1000001",
            "1111111") with
        { Sprites = [(3, 2)] };

        FrameBuffer f = RenderScene(s, 60, 60, TestData.SolidTextures(spriteTex));
        Assert.Equal(green, f[30, 30]);

        Scene behind = s with { Sprites = [(3, 5)] };
        FrameBuffer g = RenderScene(behind, 60, 60, TestData.SolidTextures(spriteTex));
        Assert.Equal(TestData.NorthColor, g[30, 30]);
    }

    [Fact]
    public void Minimap_DrawsPlayerMarker()
    {
        Scene s = TestData.SceneFromRows(2, 2, 'N', "11111", "10001", "10001", "10001", "11111");
        FrameBuffer f = RenderScene(s, 300, 300, options: new RenderOptions { Minimap = true });

        Assert.Equal(Pixel.Pack(230, 50, 50), f[50, 50]);
    }

    [Fact]
    public void RealMap_RendersWithLoadedTextures()
    {
        Scene s = SceneParser.ParseFile(TestData.MapPath("simple.cub"));
        var game = new Game(s, TextureSet.Load(s), 320, 200);
        game.Render();

        Assert.Equal(s.Ceiling.Packed, game.Frame[160, 0]);
        Assert.Equal(s.Floor.Packed, game.Frame[160, 199]);
        Assert.NotEqual(s.Ceiling.Packed, game.Frame[160, 100]);
    }

    [Fact]
    public void BonusMap_WalkToDoorPressEAndPassThrough()
    {
        Scene s = SceneParser.ParseFile(TestData.MapPath("bonus.cub"));
        var game = new Game(s, TextureSet.Load(s), 64, 40);
        var forward = new InputState(true, false, false, false, false, false);

        for (int i = 0; i < 60; i++)
        {
            game.Update(forward, 1.0 / 30);
        }

        Assert.True(game.Player.PosX < 8);
        game.Update(new InputState(false, false, false, false, false, false, UseDoor: true), 1.0 / 30);
        for (int i = 0; i < 60; i++)
        {
            game.Update(forward, 1.0 / 30);
        }

        Assert.True(game.World.IsDoorOpen(8, 4));
        Assert.True(game.Player.PosX > 9);
    }

    [Fact]
    public void Maze_PlayerWalksFromStartToChicken()
    {
        Scene s = SceneParser.ParseFile(TestData.MapPath("maze.cub"));
        var world = new World(s.Map);
        var p = Player.FromScene(s);
        (int X, int Y) goal = EasterEgg.FindCorner(s) ?? throw new InvalidOperationException("нет угла");
        List<(int X, int Y)> path = ShortestPath(world, (s.StartX, s.StartY), goal);
        Assert.True(path.Count > 10);

        foreach ((int cx, int cy) in path)
        {
            for (int i = 0; i < 400; i++)
            {
                double dx = cx + 0.5 - p.PosX, dy = cy + 0.5 - p.PosY;
                double dist = Math.Sqrt((dx * dx) + (dy * dy));
                if (dist < 0.05)
                {
                    break;
                }

                double turn = Math.Atan2((p.DirX * dy) - (p.DirY * dx), (p.DirX * dx) + (p.DirY * dy));
                p.Rotate(turn);
                p.Walk(Math.Min(0.1, dist), 0, world.IsWalkable, world.IsFree);
            }

            Assert.True(Math.Abs(cx + 0.5 - p.PosX) < 0.06 && Math.Abs(cy + 0.5 - p.PosY) < 0.06, $"застряли у клетки ({cx}, {cy})");
        }
    }

    private static List<(int X, int Y)> ShortestPath(World world, (int X, int Y) from, (int X, int Y) to)
    {
        var prev = new Dictionary<(int, int), (int, int)>();
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue(from);
        prev[from] = from;
        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();
            if ((x, y) == to)
            {
                break;
            }

            foreach ((int nx, int ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
            {
                if (world.IsWalkable(nx, ny) && prev.TryAdd((nx, ny), (x, y)))
                {
                    queue.Enqueue((nx, ny));
                }
            }
        }

        var path = new List<(int X, int Y)>();
        for ((int, int) c = to; c != from; c = prev[c])
        {
            path.Add(c);
        }

        path.Reverse();
        return path;
    }

    private static string[] BigRoom(int n)
    {
        var rows = new string[n];
        string wall = new('1', n);
        string inner = "1" + new string('0', n - 2) + "1";
        for (int i = 0; i < n; i++)
        {
            rows[i] = i == 0 || i == n - 1 ? wall : inner;
        }

        return rows;
    }
}
