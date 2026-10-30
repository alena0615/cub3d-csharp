namespace Cub3D.Tests;

public class PngTests
{
    [Fact]
    public void EncodeDecode_RoundTrip()
    {
        uint[] pixels = [Pixel.Pack(1, 2, 3), Pixel.Pack(250, 128, 0, 10), Pixel.Pack(0, 0, 0), Pixel.Pack(255, 255, 255)];
        byte[] png = Png.Encode(2, 2, pixels);

        (int w, int h, uint[] back) = Png.Decode(png);
        Assert.Equal(2, w);
        Assert.Equal(2, h);
        Assert.Equal(pixels, back);
    }

    [Theory]
    [InlineData("north.png")]
    [InlineData("south.png")]
    [InlineData("west.png")]
    [InlineData("east.png")]
    [InlineData("door.png")]
    [InlineData("sprite.png")]
    public void StarterTextures_Are64x64(string name)
    {
        Texture t = Texture.Load(TestData.TexturePath(name));
        Assert.Equal(64, t.Width);
        Assert.Equal(64, t.Height);
    }

    [Fact]
    public void SpriteTexture_HasTransparentCorner()
    {
        Texture t = Texture.Load(TestData.TexturePath("sprite.png"));
        Assert.Equal(0, Pixel.A(t[0, 0]));
    }

    [Fact]
    public void NotPng_RaisesTextureError()
    {
        Assert.Throws<TextureError>(() => Png.Decode([1, 2, 3, 4, 5, 6, 7, 8, 9]));
    }
}

[Collection("Console")]
public class CliTests
{
    [Theory]
    [InlineData("")]
    [InlineData("a.cub b.cub")]
    [InlineData("a.cub --width")]
    [InlineData("a.cub --width abc")]
    [InlineData("a.cub --height 10")]
    [InlineData("a.cub --unknown")]
    public void WrongArguments_ExitCode2(string commandLine)
    {
        string[] args = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Null(CliOptions.Parse(args));
        Assert.Equal(2, Program.Main(args));
    }

    [Fact]
    public void Options_AreParsed()
    {
        CliOptions? o = CliOptions.Parse(["m.cub", "--width", "640", "--height", "480", "--minimap"]);

        Assert.NotNull(o);
        Assert.Equal("m.cub", o.MapPath);
        Assert.Equal(640, o.Width);
        Assert.Equal(480, o.Height);
        Assert.True(o.Minimap);
        Assert.False(o.Shading);
    }

    [Theory]
    [InlineData("open_space.cub")]
    [InlineData("missing_texture.cub")]
    [InlineData("no_player.cub")]
    public void BadMap_ExitCode1(string file)
    {
        Assert.Equal(1, Program.Main([TestData.BadMapPath(file), "--bench", "1"]));
    }

    [Fact]
    public void MissingFile_ExitCode1()
    {
        Assert.Equal(1, Program.Main([TestData.MapPath("nope.cub"), "--bench", "1"]));
    }

    [Fact]
    public void Screenshot_WritesPng()
    {
        string path = Path.Combine(Path.GetTempPath(), $"cub3d-test-{Guid.NewGuid():N}.png");
        try
        {
            Assert.Equal(0, Program.Main([TestData.MapPath("simple.cub"), "--screenshot", path, "--width", "200", "--height", "120"]));
            (int w, int h, _) = Png.Load(path);
            Assert.Equal(200, w);
            Assert.Equal(120, h);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
