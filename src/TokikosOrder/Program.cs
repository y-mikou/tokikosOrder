using System.Globalization;
using SkiaSharp;

namespace TokikosOrder;

internal static class Program
{
    private const string DefaultOutputPath = "./tokikosOrder_output.jpg";
    private const double MillimetersPerInch = 25.4;

    private static int Main(string[] args)
    {
        if (args.Length is < 6 or > 7)
        {
            PrintUsage("引数は6個（出力先を指定する場合は7個）必要です。");
            return 1;
        }

        if (!TryParsePositive(args[0], "表紙サイズ・縦", out var coverHeight) ||
            !TryParsePositive(args[1], "表紙サイズ・横", out var coverWidth) ||
            !TryParseNonNegative(args[2], "背幅", out var spineWidth) ||
            !TryParseNonNegative(args[3], "断ち切り幅", out var trim) ||
            !TryParseNonNegative(args[4], "塗り足し幅", out var bleed))
        {
            return 1;
        }

        if (!int.TryParse(args[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dpi) || dpi is not (350 or 600))
        {
            Console.Error.WriteLine("解像度は350または600を指定してください。");
            return 1;
        }

        var outputPath = args.Length == 7 ? args[6] : DefaultOutputPath;
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            Console.Error.WriteLine("出力先パスを空にすることはできません。");
            return 1;
        }

        try
        {
            CreateCoverImage(coverHeight, coverWidth, spineWidth, trim, bleed, dpi, outputPath);
            Console.WriteLine($"画像を作成しました: {Path.GetFullPath(outputPath)} ({dpi} dpi)");
            return 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or OverflowException)
        {
            Console.Error.WriteLine($"画像を作成できませんでした: {ex.Message}");
            return 1;
        }
    }

    private static void CreateCoverImage(
        double coverHeight,
        double coverWidth,
        double spineWidth,
        double trim,
        double bleed,
        int dpi,
        string outputPath)
    {
        // The region table defines two cover panels plus the spine and bleed on both sides.
        if (bleed * 2 >= coverHeight || bleed * 2 >= coverWidth)
        {
            throw new ArgumentException("塗り足し幅の2倍は、表紙の高さと幅より小さくしてください。");
        }

        var totalWidthMm = (coverWidth * 2) + spineWidth + (trim * 2);
        var totalHeightMm = coverHeight + (trim * 2);
        var width = ToPixels(totalWidthMm, dpi);
        var height = ToPixels(totalHeightMm, dpi);
        if (width < 1 || height < 1)
        {
            throw new ArgumentException("出力画像の寸法が小さすぎてピクセルに変換できません。");
        }

        // Convert each boundary from millimeters independently to prevent rounding gaps.
        var xTrim = ToPixels(trim, dpi);
        var xCover1End = ToPixels(trim + coverWidth, dpi);
        var xSpineEnd = ToPixels(trim + coverWidth + spineWidth, dpi);
        var xCover2End = ToPixels(trim + (coverWidth * 2) + spineWidth, dpi);
        var yTrim = ToPixels(trim, dpi);
        var yCoverEnd = ToPixels(trim + coverHeight, dpi);

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill })
        {
            canvas.Clear(SKColors.Red);
            paint.Color = SKColors.Green;
            canvas.DrawRect(SKRect.Create(xTrim, yTrim, xCover1End - xTrim, yCoverEnd - yTrim), paint);
            paint.Color = SKColors.Blue;
            canvas.DrawRect(SKRect.Create(xCover1End, yTrim, xSpineEnd - xCover1End, yCoverEnd - yTrim), paint);
            paint.Color = SKColors.Green;
            canvas.DrawRect(SKRect.Create(xSpineEnd, yTrim, xCover2End - xSpineEnd, yCoverEnd - yTrim), paint);

            if (bleed > 0)
            {
                var xCover1FrameInnerLeft = ToPixels(trim + bleed, dpi);
                var xCover1FrameInnerRight = ToPixels(trim + coverWidth - bleed, dpi);
                var xCover2FrameInnerLeft = ToPixels(trim + coverWidth + spineWidth + bleed, dpi);
                var xCover2FrameInnerRight = ToPixels(trim + (coverWidth * 2) + spineWidth - bleed, dpi);
                var yFrameInnerTop = ToPixels(trim + bleed, dpi);
                var yFrameInnerBottom = ToPixels(trim + coverHeight - bleed, dpi);

                paint.Color = SKColors.Yellow;
                DrawFrame(canvas, paint, xTrim, xCover1End, yTrim, yCoverEnd,
                    xCover1FrameInnerLeft, xCover1FrameInnerRight, yFrameInnerTop, yFrameInnerBottom);
                paint.Color = new SKColor(255, 165, 0); // orange
                canvas.DrawRect(SKRect.Create(xCover1End, yTrim, xSpineEnd - xCover1End, yFrameInnerTop - yTrim), paint);
                canvas.DrawRect(SKRect.Create(xCover1End, yFrameInnerBottom, xSpineEnd - xCover1End, yCoverEnd - yFrameInnerBottom), paint);
                paint.Color = SKColors.Yellow;
                DrawFrame(canvas, paint, xSpineEnd, xCover2End, yTrim, yCoverEnd,
                    xCover2FrameInnerLeft, xCover2FrameInnerRight, yFrameInnerTop, yFrameInnerBottom);
            }
        }

        var fullPath = Path.GetFullPath(outputPath);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 100)
            ?? throw new InvalidOperationException("JPEG画像をエンコードできませんでした。");

        var jpegBytes = SetJfifDpi(encoded.ToArray(), dpi);
        File.WriteAllBytes(fullPath, jpegBytes);
    }

    private static void DrawFrame(
        SKCanvas canvas,
        SKPaint paint,
        int left,
        int right,
        int top,
        int bottom,
        int innerLeft,
        int innerRight,
        int innerTop,
        int innerBottom)
    {
        canvas.DrawRect(SKRect.Create(left, top, right - left, innerTop - top), paint);
        canvas.DrawRect(SKRect.Create(left, innerBottom, right - left, bottom - innerBottom), paint);
        canvas.DrawRect(SKRect.Create(left, innerTop, innerLeft - left, innerBottom - innerTop), paint);
        canvas.DrawRect(SKRect.Create(innerRight, innerTop, right - innerRight, innerBottom - innerTop), paint);
    }

    private static byte[] SetJfifDpi(byte[] jpeg, int dpi)
    {
        // JPEG density is stored as big-endian pixels-per-inch in the JFIF APP0 segment.
        for (var offset = 2; offset + 3 < jpeg.Length;)
        {
            if (jpeg[offset] != 0xFF)
            {
                break;
            }

            while (offset < jpeg.Length && jpeg[offset] == 0xFF)
            {
                offset++;
            }

            if (offset >= jpeg.Length)
            {
                break;
            }

            var marker = jpeg[offset++];
            if (marker is 0xDA or 0xD9)
            {
                break;
            }

            if (marker is >= 0xD0 and <= 0xD7 or 0x01)
            {
                continue;
            }

            if (offset + 2 > jpeg.Length)
            {
                break;
            }

            var segmentLength = (jpeg[offset] << 8) | jpeg[offset + 1];
            if (segmentLength < 2 || offset + segmentLength > jpeg.Length)
            {
                break;
            }

            var payload = offset + 2;
            if (marker == 0xE0 && segmentLength >= 16 &&
                jpeg[payload] == (byte)'J' && jpeg[payload + 1] == (byte)'F' &&
                jpeg[payload + 2] == (byte)'I' && jpeg[payload + 3] == (byte)'F' &&
                jpeg[payload + 4] == 0)
            {
                jpeg[payload + 7] = 1; // density unit: dots per inch
                jpeg[payload + 8] = (byte)(dpi >> 8);
                jpeg[payload + 9] = (byte)dpi;
                jpeg[payload + 10] = (byte)(dpi >> 8);
                jpeg[payload + 11] = (byte)dpi;
                return jpeg;
            }

            offset += segmentLength;
        }

        // If the encoder omitted JFIF metadata, insert a minimal APP0 header after SOI.
        var jfif = new byte[]
        {
            0xFF, 0xE0, 0x00, 0x10, (byte)'J', (byte)'F', (byte)'I', (byte)'F', 0,
            1, 1, 1,
            (byte)(dpi >> 8), (byte)dpi,
            (byte)(dpi >> 8), (byte)dpi,
            0, 0
        };
        var result = new byte[jpeg.Length + jfif.Length];
        Buffer.BlockCopy(jpeg, 0, result, 0, 2);
        Buffer.BlockCopy(jfif, 0, result, 2, jfif.Length);
        Buffer.BlockCopy(jpeg, 2, result, 2 + jfif.Length, jpeg.Length - 2);
        return result;
    }

    private static int ToPixels(double millimeters, int dpi)
    {
        var pixels = Math.Round(millimeters * dpi / MillimetersPerInch, MidpointRounding.AwayFromZero);
        if (pixels < 0 || pixels > int.MaxValue)
        {
            throw new ArgumentException("寸法が大きすぎるか、ピクセルに変換できません。");
        }

        return (int)pixels;
    }

    private static bool TryParsePositive(string value, string label, out double result)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
            double.IsFinite(result) && result > 0)
        {
            return true;
        }

        Console.Error.WriteLine($"{label}は0より大きい数値（mm）で指定してください: {value}");
        return false;
    }

    private static bool TryParseNonNegative(string value, string label, out double result)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) &&
            double.IsFinite(result) && result >= 0)
        {
            return true;
        }

        Console.Error.WriteLine($"{label}は0以上の数値（mm）で指定してください: {value}");
        return false;
    }

    private static void PrintUsage(string error)
    {
        Console.Error.WriteLine(error);
        Console.Error.WriteLine("使い方: TokikosOrder <表紙高さmm> <表紙幅mm> <背幅mm> <断ち切りmm> <塗り足しmm> <350|600> [出力.jpg]");
        Console.Error.WriteLine("例: TokikosOrder 210 148 12 3 3 350 ./cover.jpg");
    }
}
