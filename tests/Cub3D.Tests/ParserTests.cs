namespace Cub3D.Tests;

public class ParserTests
{
    [Fact]
    public void Simple_ReadsMapSizeStartAndColors()
    {
        Scene s = SceneParser.ParseFile(TestData.MapPath("simple.cub"));

        Assert.Equal(10, s.Map.Width);
        Assert.Equal(8, s.Map.Height);
        Assert.Equal(4, s.StartX);
        Assert.Equal(3, s.StartY);
        Assert.Equal('N', s.StartDirection);
        Assert.Equal(new Rgb(60, 60, 70), s.Floor);
        Assert.Equal(new Rgb(150, 190, 230), s.Ceiling);
    }

    [Fact]
    public void Simple_ResolvesTexturePathsAndMapCells()
    {
        Scene s = SceneParser.ParseFile(TestData.MapPath("simple.cub"));

        Assert.EndsWith("north.png", s.North, StringComparison.Ordinal);
        Assert.EndsWith("south.png", s.South, StringComparison.Ordinal);
        Assert.EndsWith("west.png", s.West, StringComparison.Ordinal);
        Assert.EndsWith("east.png", s.East, StringComparison.Ordinal);
        Assert.True(File.Exists(s.North));

        Assert.Equal(Cell.Floor, s.Map[4, 3]);
        Assert.Equal(Cell.Wall, s.Map[3, 2]);
        Assert.Equal(Cell.Wall, s.Map[0, 0]);
        Assert.Null(s.Door);
        Assert.Empty(s.Sprites);
    }

    [Fact]
    public void ElementsInAnyOrderWithBlankLinesAndSpacesInColors()
    {
        Scene s = SceneParser.ParseFile(TestData.MapPath("42.cub"));

        Assert.Equal(new Rgb(70, 65, 60), s.Floor);
        Assert.Equal(new Rgb(135, 180, 225), s.Ceiling);
        Assert.Equal('N', s.StartDirection);
    }

    [Fact]
    public void IrregularMap_IsValidAndPaddedWithVoid()
    {
        Scene s = SceneParser.ParseFile(TestData.MapPath("irregular.cub"));

        Assert.Equal(20, s.Map.Width);
        Assert.Equal(Cell.Void, s.Map[0, 0]);
        Assert.Equal(Cell.Void, s.Map[19, 0]);
        Assert.Equal('E', s.StartDirection);
    }

    [Fact]
    public void BonusMap_ReadsDoorsAndSprites()
    {
        Scene s = SceneParser.ParseFile(TestData.MapPath("bonus.cub"));

        Assert.NotNull(s.Door);
        Assert.NotNull(s.Sprite);
        Assert.Equal(Cell.Door, s.Map[8, 4]);
        Assert.Equal(5, s.Sprites.Count);
        Assert.Equal(Cell.Floor, s.Map[2, 2]);
    }

    [Fact]
    public void Maze_IsValid()
    {
        Scene s = SceneParser.ParseFile(TestData.MapPath("maze.cub"));
        Assert.Equal(31, s.Map.Width);
        Assert.Equal(21, s.Map.Height);
        Assert.Equal('N', s.StartDirection);
        Assert.Equal(Cell.Floor, s.Map[s.StartX, s.StartY - 1]);
    }

    [Theory]
    [InlineData("open_edge.cub", "карта не замкнута: клетка в строке 10, колонке 6 лежит на краю карты")]
    [InlineData("open_space.cub", "карта не замкнута: клетка в строке 9, колонке 4 граничит с пустотой")]
    [InlineData("bad_char.cub", "строка 9, колонка 4: недопустимый символ '2' в карте")]
    [InlineData("duplicate_element.cub", "строка 6: элемент F задан повторно")]
    [InlineData("missing_element.cub", "строка 7: карта начинается до того, как заданы все элементы (не заданы: EA)")]
    [InlineData("no_player.cub", "на карте нет стартовой позиции игрока")]
    [InlineData("two_players.cub", "строка 9, колонка 4: игрок задан повторно")]
    [InlineData("color_range.cub", "строка 5: компонента цвета должна быть целым числом 0..255, получено '300'")]
    [InlineData("color_format.cub", "строка 5: цвет должен состоять из трёх чисел через запятую")]
    [InlineData("missing_texture.cub", "строка 1: файл текстуры не найден")]
    [InlineData("empty_line_in_map.cub", "строка 10: пустая строка внутри карты")]
    [InlineData("door_without_do.cub", "строка 9, колонка 4: дверь 'D' на карте требует элемента DO")]
    [InlineData("unknown_element.cub", "строка 7: неизвестный идентификатор 'R'")]
    [InlineData("no_map.cub", "в файле нет карты")]
    [InlineData("not_png.cub", "строка 1: текстура должна быть файлом PNG")]
    [InlineData("wrong_extension.txt", "расширение .cub")]
    public void BadMaps_RaiseSceneFormatErrorWithReason(string file, string expected)
    {
        var e = Assert.Throws<SceneFormatError>(() => SceneParser.ParseFile(TestData.BadMapPath(file)));
        Assert.Contains(expected, e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SceneFormatError_IsCubError()
    {
        Assert.True(typeof(CubError).IsAssignableFrom(typeof(SceneFormatError)));
    }

    [Fact]
    public void MissingFile_IsStandardIoError()
    {
        Assert.Throws<FileNotFoundException>(() => SceneParser.ParseFile(TestData.MapPath("nope.cub")));
    }

    [Fact]
    public void Parse_FromTextWithoutTextureCheck()
    {
        const string text = "NO a.png\nSO b.png\nWE c.png\nEA d.png\nF 1,2,3\nC 4,5,6\n111\n1W1\n111\n\n\n";
        Scene s = SceneParser.Parse(text, checkTextures: false);

        Assert.Equal('W', s.StartDirection);
        Assert.Equal(3, s.Map.Height);
    }

    [Theory]
    [InlineData("0,0,0", 0, 0, 0)]
    [InlineData("255,255,255", 255, 255, 255)]
    [InlineData(" 60 , 60 ,70 ", 60, 60, 70)]
    [InlineData("007,8,9", 7, 8, 9)]
    public void ParseColor_Valid(string text, int r, int g, int b)
    {
        Assert.Equal(new Rgb((byte)r, (byte)g, (byte)b), SceneParser.ParseColor(text));
    }

    [Theory]
    [InlineData("256,0,0")]
    [InlineData("-1,0,0")]
    [InlineData("1,2")]
    [InlineData("1,2,3,4")]
    [InlineData("a,b,c")]
    [InlineData("1,,3")]
    [InlineData("1.5,2,3")]
    [InlineData("")]
    public void ParseColor_Invalid(string text)
    {
        Assert.Throws<SceneFormatError>(() => SceneParser.ParseColor(text));
    }
}
