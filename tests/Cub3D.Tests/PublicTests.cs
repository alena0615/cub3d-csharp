namespace Cub3D.Tests;

[Collection("Console")]
public class PublicTests
{
    private const int P = 9;

    [Fact]
    public void S3_ParseScene_SimpleExample()
    {
        Scene s = SceneParser.ParseFile(TestData.MapPath("simple.cub"));

        Assert.EndsWith("north.png", s.North, StringComparison.Ordinal);
        Assert.EndsWith("south.png", s.South, StringComparison.Ordinal);
        Assert.EndsWith("west.png", s.West, StringComparison.Ordinal);
        Assert.EndsWith("east.png", s.East, StringComparison.Ordinal);
        Assert.Equal(new Rgb(60, 60, 70), s.Floor);
        Assert.Equal(new Rgb(150, 190, 230), s.Ceiling);
        Assert.Equal((4, 3, 'N'), (s.StartX, s.StartY, s.StartDirection));
        Assert.Equal(Cell.Floor, s.Map[4, 3]);
    }

    [Fact]
    public void S3_ElementsInAnyOrder_BlankLinesBetween_SpacesAroundColorNumbers()
    {
        const string text = "C 1, 2 ,3\n\nF 4,5,6\nEA e.png\n\nNO n.png\nWE w.png\nSO s.png\n\n111\n1E1\n111\n";
        Scene s = SceneParser.Parse(text, checkTextures: false);

        Assert.Equal(new Rgb(1, 2, 3), s.Ceiling);
        Assert.Equal(new Rgb(4, 5, 6), s.Floor);
        Assert.Equal('E', s.StartDirection);
    }

    [Fact]
    public void S3_ShortRowsArePaddedWithVoid()
    {
        Scene s = SceneParser.ParseFile(TestData.MapPath("irregular.cub"));
        Assert.Equal(Cell.Void, s.Map[s.Map.Width - 1, 0]);
    }

    [Fact]
    public void S3_IrregularMapIsValid_OpenMapsAreNot()
    {
        SceneParser.ParseFile(TestData.MapPath("irregular.cub"));
        Assert.Throws<SceneFormatError>(() => SceneParser.ParseFile(TestData.BadMapPath("open_edge.cub")));
        Assert.Throws<SceneFormatError>(() => SceneParser.ParseFile(TestData.BadMapPath("open_space.cub")));
    }

    [Fact]
    public void S3_WalkableCellTouchingVoidDiagonally_IsError()
    {
        const string head = "NO n.png\nSO s.png\nWE w.png\nEA e.png\nF 1,1,1\nC 2,2,2\n";
        const string map = "11111\n1N001\n10001\n1001 \n11111\n";
        var e = Assert.Throws<SceneFormatError>(() => SceneParser.Parse(head + map, checkTextures: false));
        Assert.Contains("карта не замкнута", e.Message, StringComparison.Ordinal);
        Assert.Contains("колонке 4 граничит с пустотой", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void S3_DoorWithoutDO_AndSpriteWithoutSP_AreErrors()
    {
        const string head = "NO n.png\nSO s.png\nWE w.png\nEA e.png\nF 1,1,1\nC 2,2,2\n";
        Assert.Throws<SceneFormatError>(() => SceneParser.Parse(head + "11111\n1N0D1\n11111\n", checkTextures: false));
        Assert.Throws<SceneFormatError>(() => SceneParser.Parse(head + "11111\n1N0X1\n11111\n", checkTextures: false));

        Scene ok = SceneParser.Parse("DO d.png\nSP x.png\n" + head + "1111111\n1N0D0X1\n1111111\n", checkTextures: false);
        Assert.Equal(Cell.Door, ok.Map[3, 1]);
        Assert.Equal((5, 1), Assert.Single(ok.Sprites));
    }

    [Fact]
    public void S3_SceneFormatError_IsCubError_WithLineAndColumn()
    {
        Assert.True(typeof(CubError).IsAssignableFrom(typeof(SceneFormatError)));
        var e = Assert.Throws<SceneFormatError>(() => SceneParser.ParseFile(TestData.BadMapPath("bad_char.cub")));
        Assert.Equal("строка 9, колонка 4: недопустимый символ '2' в карте", e.Message);
    }

    [Fact]
    public void S3_MissingFile_IsStandardIoError_NotCaughtByParser()
    {
        Assert.Throws<FileNotFoundException>(() => SceneParser.ParseFile(TestData.MapPath("no_such_file.cub")));
    }

    [Fact]
    public void S3_MissingTextureFile_IsFormatError()
    {
        var e = Assert.Throws<SceneFormatError>(() => SceneParser.ParseFile(TestData.BadMapPath("missing_texture.cub")));
        Assert.Contains("строка 1", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void S41_StartVectors_NorthAndEast()
    {
        var n = Player.FromStart(0, 0, 'N');
        Assert.Equal((0.0, -1.0), (n.DirX, n.DirY));
        Assert.Equal(0.65, n.PlaneX, 2);
        Assert.Equal(0.0, n.PlaneY, P);

        var e = Player.FromStart(0, 0, 'E');
        Assert.Equal((1.0, 0.0), (e.DirX, e.DirY));
        Assert.Equal(0.0, e.PlaneX, P);
        Assert.Equal(0.65, e.PlaneY, 2);
    }

    [Fact]
    public void S41_StartPosition_IsCellCenter_Fov66()
    {
        var p = Player.FromStart(3, 7, 'S');
        Assert.Equal(3.5, p.PosX, P);
        Assert.Equal(7.5, p.PosY, P);
        Assert.Equal(66.0, Player.FovDegrees, P);
        Assert.Equal(Math.Tan(33 * Math.PI / 180), Player.PlaneLength, P);
    }

    [Fact]
    public void S41_CameraX_And_RayDirection()
    {
        Assert.Equal(-1.0, Raycaster.CameraX(0, 960), P);
        Assert.Equal(0.0, Raycaster.CameraX(480, 960), P);

        var p = Player.FromStart(0, 0, 'N');
        (double rx, double ry) = Raycaster.RayDirection(p, -1);
        Assert.Equal(-Player.PlaneLength, rx, P);
        Assert.Equal(-1.0, ry, P);
    }

    [Theory]
    [InlineData(0, 1.0, 0.3, Facing.East)]
    [InlineData(0, -1.0, 0.3, Facing.West)]
    [InlineData(1, 0.3, 1.0, Facing.South)]
    [InlineData(1, 0.3, -1.0, Facing.North)]
    public void S42_WallFacingConvention(int side, double rayX, double rayY, Facing expected)
    {
        Assert.Equal(expected, Raycaster.WallFacing(side, rayX, rayY));
    }

    [Fact]
    public void S42_Figure4_Example()
    {
        var world = new World(GameMap.FromRows("00001", "00000", "00000"));
        RayHit h = Raycaster.CastRay(world, 0.7, 2.6, 1, -0.5);

        Assert.Equal((4, 0), (h.MapX, h.MapY));
        Assert.Equal(0, h.Side);
        Assert.Equal(3.3, h.Distance, P);
        Assert.Equal(0.95, h.WallX, P);
        Assert.Equal(Facing.East, h.Facing);
    }

    [Fact]
    public void S42_DistanceIsPerpendicular_WallXIsFraction()
    {
        var world = new World(GameMap.FromRows("11111", "10001", "10001", "10001", "11111"));
        RayHit h = Raycaster.CastRay(world, 2.5, 2.5, 1, 0.5);

        Assert.Equal(1.5, h.Distance, P);
        Assert.Equal(0.25, h.WallX, P);
    }

    [Fact]
    public void S42_RayDoesNotHang()
    {
        var world = new World(GameMap.FromRows("0"));
        RayHit h = Raycaster.CastRay(world, 0.5, 0.5, 1, 0.37);
        Assert.True(h.Distance < 10);
    }

    [Fact]
    public void S43_LineHeight_IsHOverDist()
    {
        Assert.Equal(600.0, Raycaster.LineHeight(600, 1), P);
        Assert.Equal(300.0, Raycaster.LineHeight(600, 2), P);
    }

    [Theory]
    [InlineData(Facing.North, 0.25, 16)]
    [InlineData(Facing.East, 0.25, 16)]
    [InlineData(Facing.South, 0.25, 47)]
    [InlineData(Facing.West, 0.25, 47)]
    [InlineData(Facing.North, 0.0, 0)]
    [InlineData(Facing.South, 0.0, 63)]
    public void S44_TextureColumn_MirroredForSouthAndWest(Facing facing, double wallX, int expected)
    {
        Assert.Equal(expected, Raycaster.TextureColumn(wallX, facing, 64));
    }

    [Fact]
    public void S45_RotatePositiveIsRight_KeepsLength()
    {
        var p = Player.FromStart(0, 0, 'N');
        p.Rotate(Math.PI / 2);

        Assert.Equal(1.0, p.DirX, P);
        Assert.Equal(0.0, p.DirY, P);
        Assert.Equal(Player.PlaneLength, Math.Sqrt((p.PlaneX * p.PlaneX) + (p.PlaneY * p.PlaneY)), P);
    }

    [Fact]
    public void S45_StrafeRight_IsMinusDirYDirX()
    {
        var world = new World(GameMap.FromRows("11111", "10001", "10001", "10001", "11111"));
        var p = Player.FromStart(2, 2, 'N');
        p.Walk(0, 0.5, world.IsWalkable);

        Assert.Equal(3.0, p.PosX, P);
        Assert.Equal(2.5, p.PosY, P);
    }

    [Fact]
    public void S45_CollisionMargin_AndSlidingPerAxis()
    {
        Assert.Equal(0.2, Player.CollisionMargin, P);
        var world = new World(GameMap.FromRows("11111", "10001", "10001", "10001", "11111"));
        var p = Player.FromStart(2, 1, 'N');

        p.Move(0.2, -0.4, world.IsWalkable);
        Assert.Equal(2.7, p.PosX, P);
        Assert.Equal(1.5, p.PosY, P);
    }

    [Fact]
    public void S2_WrongArgumentCount_ExitCode2()
    {
        Assert.Equal(2, Program.Main([]));
        Assert.Equal(2, Program.Main(["a.cub", "b.cub"]));
    }

    [Theory]
    [InlineData("open_edge.cub")]
    [InlineData("open_space.cub")]
    [InlineData("bad_char.cub")]
    [InlineData("missing_texture.cub")]
    [InlineData("no_player.cub")]
    public void S2_BadMap_PrintsErrorThenReason_ExitCode1(string file)
    {
        TextWriter old = Console.Error;
        using var err = new StringWriter();
        Console.SetError(err);
        try
        {
            Assert.Equal(1, Program.Main([TestData.BadMapPath(file), "--bench", "1"]));
        }
        finally
        {
            Console.SetError(old);
        }

        string[] lines = err.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Error", lines[0].TrimEnd('\r'));
        Assert.True(lines.Length >= 2 && lines[1].Trim().Length > 0);
    }
}

[CollectionDefinition("Console", DisableParallelization = true)]
public class ConsoleTestsDefinition
{
}
