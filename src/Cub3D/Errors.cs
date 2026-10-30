namespace Cub3D;

#pragma warning disable CA1710

public class CubError : Exception
{
    public CubError() { }

    public CubError(string message)
        : base(message) { }

    public CubError(string message, Exception inner)
        : base(message, inner) { }
}

public class SceneFormatError : CubError
{
    public SceneFormatError() { }

    public SceneFormatError(string message)
        : base(message) { }

    public SceneFormatError(string message, Exception inner)
        : base(message, inner) { }

    public static SceneFormatError AtLine(int line, string reason) => new($"строка {line}: {reason}");

    public static SceneFormatError At(int line, int column, string reason) =>
        new($"строка {line}, колонка {column}: {reason}");
}

public class TextureError : CubError
{
    public TextureError() { }

    public TextureError(string message)
        : base(message) { }

    public TextureError(string message, Exception inner)
        : base(message, inner) { }
}
