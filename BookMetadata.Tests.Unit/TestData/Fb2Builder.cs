using System.IO.Compression;
using System.Text;
using SkiaSharp;

namespace BookMetadata.Tests.Unit.TestData;

public static class Fb2Builder
{
    public static string CreateXml(
        string title,
        IEnumerable<(string First, string Last)> authors,
        string? isbn = null,
        bool withNamespace = true,
        string? coverHref = null,
        byte[]? coverImage = null)
    {
        var ns = withNamespace ? " xmlns=\"http://www.gribuser.ru/xml/fictionbook/2.0\"" : string.Empty;
        var xlink = coverHref is not null ? " xmlns:l=\"http://www.w3.org/1999/xlink\"" : string.Empty;
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine($"<FictionBook{ns}{xlink}>");
        sb.AppendLine("  <description>");
        sb.AppendLine("    <title-info>");
        sb.AppendLine($"      <book-title>{title}</book-title>");
        foreach (var (first, last) in authors)
        {
            sb.AppendLine("      <author>");
            sb.AppendLine($"        <first-name>{first}</first-name>");
            sb.AppendLine($"        <last-name>{last}</last-name>");
            sb.AppendLine("      </author>");
        }

        if (coverHref is not null)
        {
            sb.AppendLine("      <coverpage>");
            sb.AppendLine($"        <image l:href=\"#{coverHref}\"/>");
            sb.AppendLine("      </coverpage>");
        }

        sb.AppendLine("    </title-info>");
        if (!string.IsNullOrEmpty(isbn))
        {
            sb.AppendLine("    <publish-info>");
            sb.AppendLine($"      <isbn>{isbn}</isbn>");
            sb.AppendLine("    </publish-info>");
        }

        sb.AppendLine("  </description>");
        if (coverImage is not null && coverHref is not null)
        {
            sb.AppendLine($"  <binary id=\"{coverHref}\" content-type=\"image/png\">{Convert.ToBase64String(coverImage)}</binary>");
        }

        sb.AppendLine("</FictionBook>");
        return sb.ToString();
    }

    public static string CreateMinimalXml(bool withNamespace = true)
    {
        var ns = withNamespace ? " xmlns=\"http://www.gribuser.ru/xml/fictionbook/2.0\"" : string.Empty;
        return $"<FictionBook{ns}></FictionBook>";
    }

    public static byte[] CreatePng()
    {
        using var surface = SKSurface.Create(new SKImageInfo(2, 2));
        surface.Canvas.Clear(SKColors.CornflowerBlue);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public static byte[] CreateSvg()
        => Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1\" height=\"1\"/>");

    public static Stream ToStream(string xml)
    {
        var ms = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        ms.Position = 0;
        return ms;
    }

    public static Stream CreateZip(string innerFileName, string xml)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry(innerFileName);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(xml);
        }

        ms.Position = 0;
        return ms;
    }
}