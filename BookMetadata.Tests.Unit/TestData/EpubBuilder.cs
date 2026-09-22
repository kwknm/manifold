using System.IO.Compression;
using System.Text;

namespace BookMetadata.Tests.Unit.TestData;

public static class EpubBuilder
{
    private const string ContainerXml =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
        "<container version=\"1.0\" xmlns=\"urn:oasis:names:tc:opendocument:xmlns:container\">\n" +
        "  <rootfiles>\n" +
        "    <rootfile full-path=\"content.opf\" media-type=\"application/oebps-package+xml\"/>\n" +
        "  </rootfiles>\n" +
        "</container>\n";

    public static Stream Create(
        string title,
        string author,
        string isbn,
        string chapterText,
        byte[]? coverImage = null,
        string coverExtension = "png",
        string? coverMetaContent = null,
        bool coverMetaAsHref = false)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "mimetype", "application/epub+zip", noCompression: true);
            WriteEntry(zip, "META-INF/container.xml", ContainerXml);

            var coverFileName = coverImage is not null ? $"cover.{coverExtension}" : null;
            WriteEntry(zip, "content.opf", CreateOpf(title, author, isbn, coverFileName, coverMetaContent, coverMetaAsHref));

            if (coverImage is not null)
            {
                WriteEntry(zip, coverFileName!, coverImage);
            }

            WriteEntry(zip, "toc.ncx", CreateNcx(title));
            WriteEntry(zip, "chapter1.xhtml", CreateXhtml(title, chapterText));
        }

        ms.Position = 0;
        return ms;
    }

    private static void WriteEntry(ZipArchive zip, string name, string content, bool noCompression = false)
    {
        WriteEntry(zip, name, Encoding.UTF8.GetBytes(content), noCompression);
    }

    private static void WriteEntry(ZipArchive zip, string name, byte[] content, bool noCompression = false)
    {
        var entry = zip.CreateEntry(name, noCompression ? CompressionLevel.NoCompression : CompressionLevel.Optimal);
        using var writer = entry.Open();
        writer.Write(content);
    }

    private static string CreateOpf(
        string title,
        string author,
        string isbn,
        string? coverFileName = null,
        string? coverMetaContent = null,
        bool coverMetaAsHref = false)
    {
        var hasCoverMeta = coverFileName is not null || coverMetaContent is not null;
        var metaContent = coverMetaContent
            ?? (coverMetaAsHref ? coverFileName : "cover-img");

        var meta = new StringBuilder();
        meta.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        meta.AppendLine("<package xmlns=\"http://www.idpf.org/2007/opf\" version=\"2.0\" unique-identifier=\"bookid\">");
        meta.AppendLine("  <metadata xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:opf=\"http://www.idpf.org/2007/opf\">");
        meta.AppendLine($"    <dc:title>{title}</dc:title>");
        meta.AppendLine($"    <dc:creator>{author}</dc:creator>");
        meta.AppendLine($"    <dc:identifier id=\"bookid\" opf:scheme=\"ISBN\">{isbn}</dc:identifier>");
        if (hasCoverMeta)
        {
            meta.AppendLine($"    <meta name=\"cover\" content=\"{metaContent}\"/>");
        }
        meta.AppendLine("  </metadata>");
        meta.AppendLine("  <manifest>");
        meta.AppendLine("    <item id=\"ncx\" href=\"toc.ncx\" media-type=\"application/x-dtbncx+xml\"/>");
        meta.AppendLine("    <item id=\"ch1\" href=\"chapter1.xhtml\" media-type=\"application/xhtml+xml\"/>");
        if (coverFileName is not null)
        {
            meta.AppendLine($"    <item id=\"cover-img\" href=\"{coverFileName}\" media-type=\"{GetMediaType(coverFileName)}\"/>");
        }
        meta.AppendLine("  </manifest>");
        meta.AppendLine("  <spine toc=\"ncx\">");
        meta.AppendLine("    <itemref idref=\"ch1\"/>");
        meta.AppendLine("  </spine>");
        meta.AppendLine("</package>");
        return meta.ToString();
    }

    private static string GetMediaType(string fileName)
    {
        return fileName.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
            ? "image/svg+xml"
            : "image/png";
    }

    private static string CreateNcx(string title) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
        "<ncx xmlns=\"http://www.daisy.org/z3986/2005/ncx/\" version=\"2005-1\">\n" +
        "  <head>\n" +
        "    <meta name=\"dtb:uid\" content=\"bookid\"/>\n" +
        "  </head>\n" +
        $"  <docTitle><text>{title}</text></docTitle>\n" +
        "  <navMap>\n" +
        "    <navPoint id=\"np-1\" playOrder=\"1\">\n" +
        "      <navLabel><text>Chapter 1</text></navLabel>\n" +
        "      <content src=\"chapter1.xhtml\"/>\n" +
        "    </navPoint>\n" +
        "  </navMap>\n" +
        "</ncx>\n";

    private static string CreateXhtml(string title, string text) =>
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
        "<html xmlns=\"http://www.w3.org/1999/xhtml\">\n" +
        $"  <head><title>{title}</title></head>\n" +
        $"  <body><p>{text}</p></body>\n" +
        "</html>\n";
}