using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace Cub3D;

public static class Png
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];
    private static readonly uint[] CrcTable = BuildCrcTable();

    public static (int Width, int Height, uint[] Pixels) Load(string path)
    {
        byte[] data = File.ReadAllBytes(path);
        try
        {
            return Decode(data);
        }
        catch (TextureError e)
        {
            throw new TextureError($"{path}: {e.Message}", e);
        }
    }

    public static (int Width, int Height, uint[] Pixels) Decode(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length < 8 || !data.AsSpan(0, 8).SequenceEqual(Signature))
        {
            throw new TextureError("файл не является изображением PNG");
        }

        int width = 0, height = 0, bitDepth = 0, colorType = -1;
        byte[] palette = [];
        byte[] paletteAlpha = [];
        using var idat = new MemoryStream();

        int pos = 8;
        while (pos + 8 <= data.Length)
        {
            int len = BinaryPrimitives.ReadInt32BigEndian(data.AsSpan(pos));
            string type = Encoding.ASCII.GetString(data, pos + 4, 4);
            if (len < 0 || pos + 12 + len > data.Length)
            {
                throw new TextureError("повреждённый блок PNG");
            }

            ReadOnlySpan<byte> body = data.AsSpan(pos + 8, len);
            switch (type)
            {
                case "IHDR":
                    width = BinaryPrimitives.ReadInt32BigEndian(body);
                    height = BinaryPrimitives.ReadInt32BigEndian(body[4..]);
                    bitDepth = body[8];
                    colorType = body[9];
                    if (body[12] != 0)
                    {
                        throw new TextureError("чересстрочные PNG не поддерживаются");
                    }

                    break;
                case "PLTE":
                    palette = body.ToArray();
                    break;
                case "tRNS":
                    paletteAlpha = body.ToArray();
                    break;
                case "IDAT":
                    idat.Write(body);
                    break;
                case "IEND":
                    pos = data.Length;
                    continue;
                default:
                    break;
            }

            pos += 12 + len;
        }

        if (width <= 0 || height <= 0 || width > 16384 || height > 16384)
        {
            throw new TextureError("неверный размер изображения PNG");
        }

        int channels = colorType switch
        {
            0 => 1,
            2 => 3,
            3 => 1,
            4 => 2,
            6 => 4,
            _ => throw new TextureError($"неподдерживаемый цветовой тип PNG: {colorType}"),
        };
        bool supported = bitDepth == 8 || (colorType == 3 && bitDepth is 1 or 2 or 4);
        if (!supported)
        {
            throw new TextureError($"неподдерживаемая глубина цвета PNG: {bitDepth} бит");
        }

        int bitsPerPixel = channels * bitDepth;
        int stride = ((width * bitsPerPixel) + 7) / 8;
        int bpp = Math.Max(1, bitsPerPixel / 8);
        byte[] raw = Inflate(idat.ToArray(), (stride + 1) * height);
        Unfilter(raw, stride, height, bpp);

        var pixels = new uint[width * height];
        for (int y = 0; y < height; y++)
        {
            int row = (y * (stride + 1)) + 1;
            for (int x = 0; x < width; x++)
            {
                pixels[(y * width) + x] = colorType switch
                {
                    0 => Gray(raw[row + x]),
                    2 => Pixel.Pack(raw[row + (3 * x)], raw[row + (3 * x) + 1], raw[row + (3 * x) + 2]),
                    4 => Pixel.Pack(raw[row + (2 * x)], raw[row + (2 * x)], raw[row + (2 * x)], raw[row + (2 * x) + 1]),
                    6 => Pixel.Pack(raw[row + (4 * x)], raw[row + (4 * x) + 1], raw[row + (4 * x) + 2], raw[row + (4 * x) + 3]),
                    _ => PaletteColor(raw, row, x, bitDepth, palette, paletteAlpha),
                };
            }
        }

        return (width, height, pixels);
    }

    public static byte[] Encode(int width, int height, ReadOnlySpan<uint> pixels)
    {
        if (pixels.Length != width * height)
        {
            throw new ArgumentException("размер массива пикселей не совпадает с размером изображения", nameof(pixels));
        }

        int stride = width * 4;
        var raw = new byte[(stride + 1) * height];
        for (int y = 0; y < height; y++)
        {
            int row = y * (stride + 1);
            raw[row] = 0;
            for (int x = 0; x < width; x++)
            {
                uint p = pixels[(y * width) + x];
                BinaryPrimitives.WriteUInt32LittleEndian(raw.AsSpan(row + 1 + (4 * x)), p);
            }
        }

        using var compressed = new MemoryStream();
        using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(raw);
        }

        var ihdr = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr.AsSpan(4), height);
        ihdr[8] = 8;
        ihdr[9] = 6;

        using var output = new MemoryStream();
        output.Write(Signature);
        WriteChunk(output, "IHDR", ihdr);
        WriteChunk(output, "IDAT", compressed.ToArray());
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    public static void Save(string path, int width, int height, ReadOnlySpan<uint> pixels) =>
        File.WriteAllBytes(path, Encode(width, height, pixels));

    private static uint Gray(byte v) => Pixel.Pack(v, v, v);

    private static uint PaletteColor(byte[] raw, int row, int x, int bitDepth, byte[] palette, byte[] alpha)
    {
        int index;
        if (bitDepth == 8)
        {
            index = raw[row + x];
        }
        else
        {
            int perByte = 8 / bitDepth;
            int b = raw[row + (x / perByte)];
            int shift = 8 - (bitDepth * ((x % perByte) + 1));
            index = (b >> shift) & ((1 << bitDepth) - 1);
        }

        if ((3 * index) + 2 >= palette.Length)
        {
            throw new TextureError("индекс за пределами палитры PNG");
        }

        byte a = index < alpha.Length ? alpha[index] : (byte)255;
        return Pixel.Pack(palette[3 * index], palette[(3 * index) + 1], palette[(3 * index) + 2], a);
    }

    private static byte[] Inflate(byte[] compressed, int expected)
    {
        try
        {
            using var input = new MemoryStream(compressed);
            using var z = new ZLibStream(input, CompressionMode.Decompress);
            var result = new byte[expected];
            int total = 0;
            while (total < expected)
            {
                int n = z.Read(result, total, expected - total);
                if (n == 0)
                {
                    throw new TextureError("данные PNG обрываются");
                }

                total += n;
            }

            return result;
        }
        catch (InvalidDataException e)
        {
            throw new TextureError("повреждённые сжатые данные PNG", e);
        }
    }

    private static void Unfilter(byte[] raw, int stride, int height, int bpp)
    {
        for (int y = 0; y < height; y++)
        {
            int row = (y * (stride + 1)) + 1;
            int prev = row - (stride + 1);
            byte filter = raw[row - 1];
            for (int i = 0; i < stride; i++)
            {
                int a = i >= bpp ? raw[row + i - bpp] : 0;
                int b = y > 0 ? raw[prev + i] : 0;
                int c = y > 0 && i >= bpp ? raw[prev + i - bpp] : 0;
                int add = filter switch
                {
                    0 => 0,
                    1 => a,
                    2 => b,
                    3 => (a + b) / 2,
                    4 => Paeth(a, b, c),
                    _ => throw new TextureError($"неизвестный фильтр строки PNG: {filter}"),
                };
                raw[row + i] = (byte)(raw[row + i] + add);
            }
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c;
        int pa = Math.Abs(p - a);
        int pb = Math.Abs(p - b);
        int pc = Math.Abs(p - c);
        if (pa <= pb && pa <= pc)
        {
            return a;
        }

        return pb <= pc ? b : c;
    }

    private static void WriteChunk(Stream s, string type, byte[] body)
    {
        Span<byte> len = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(len, body.Length);
        s.Write(len);
        byte[] typeBytes = Encoding.ASCII.GetBytes(type);
        s.Write(typeBytes);
        s.Write(body);
        uint crc = Crc(typeBytes, body);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc);
        s.Write(crcBytes);
    }

    private static uint Crc(byte[] type, byte[] body)
    {
        uint c = 0xFFFFFFFF;
        foreach (byte b in type)
        {
            c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        }

        foreach (byte b in body)
        {
            c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
        }

        return c ^ 0xFFFFFFFF;
    }

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            uint c = n;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }
}
