namespace Cub3D;

public readonly record struct Rgb(byte R, byte G, byte B)
{
    public uint Packed => Pixel.Pack(R, G, B);
}

public static class Pixel
{
    public static uint Pack(byte r, byte g, byte b, byte a = 255) =>
        r | ((uint)g << 8) | ((uint)b << 16) | ((uint)a << 24);

    public static byte R(uint p) => (byte)p;

    public static byte G(uint p) => (byte)(p >> 8);

    public static byte B(uint p) => (byte)(p >> 16);

    public static byte A(uint p) => (byte)(p >> 24);

    public static uint Shade(uint p, int k256)
    {
        uint r = (p & 0xFF) * (uint)k256 >> 8;
        uint g = ((p >> 8) & 0xFF) * (uint)k256 >> 8;
        uint b = ((p >> 16) & 0xFF) * (uint)k256 >> 8;
        return r | (g << 8) | (b << 16) | (p & 0xFF000000);
    }
}
