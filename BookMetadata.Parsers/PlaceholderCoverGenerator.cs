using SkiaSharp;

namespace BookMetadata.Parsers;

public static class PlaceholderCoverGenerator
{
    private const int Width = 600;
    private const int Height = 900;
    private const int JpegQuality = 85;

    public static byte[] Create(string title, string author)
    {
        var hash = Hash(title + "\n" + author);

        var hue = hash % 360f;
        var hueShift = ((hash >> 8) % 60) + 30f;

        using var surface = SKSurface.Create(new SKImageInfo(Width, Height));
        var canvas = surface.Canvas;

        canvas.Clear(SKColor.FromHsl(hue, 45, 30));

        DrawShapes(canvas, hash, hue, hueShift);

        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return data.ToArray();
    }

    private static void DrawShapes(SKCanvas canvas, uint hash, float hue, float hueShift)
    {
        var angle = ((int)((hash >> 16) % 41)) - 20;

        using var lightPaint = new SKPaint();
        lightPaint.Color = SKColor.FromHsl((hue + hueShift) % 360, 50, 55).WithAlpha(150);
        DrawShape(canvas, angle, lightPaint, x: -150, y: -350, w: 500, h: 380);

        using var darkPaint = new SKPaint();
        darkPaint.Color = SKColor.FromHsl((hue + hueShift / 2) % 360, 40, 18).WithAlpha(130);
        DrawShape(canvas, -angle, darkPaint, x: 250, y: 620, w: 520, h: 420);

        using var accentPaint = new SKPaint();
        accentPaint.Color = SKColor.FromHsl((hue + hueShift / 2) % 360, 60, 70).WithAlpha(90);
        canvas.DrawCircle(Width - 110, 130, 90, accentPaint);
    }

    private static void DrawShape(SKCanvas canvas, float angle, SKPaint paint, float x, float y, float w, float h)
    {
        canvas.Save();
        canvas.RotateDegrees(angle, x + w / 2, y + h / 2);
        canvas.DrawRect(x, y, w, h, paint);
        canvas.Restore();
    }

    private static uint Hash(string value)
    {
        const uint fnvPrime = 16777619;

        var hash = 2166136261;
        foreach (var c in value.Trim().ToLowerInvariant())
        {
            hash ^= c;
            hash *= fnvPrime;
        }

        return hash;
    }
}