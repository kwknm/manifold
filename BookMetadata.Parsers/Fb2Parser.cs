using System.IO.Compression;
using System.Xml.Linq;
using ErrorOr;

namespace BookMetadata.Parsers;

public class Fb2Parser : IBookParser
{
    public bool CanParse(string fileName)
        => fileName.EndsWith(".fb2", StringComparison.OrdinalIgnoreCase) ||
           fileName.EndsWith(".fb2.zip", StringComparison.OrdinalIgnoreCase);

    public ErrorOr<BookMetadata> Parse(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        try
        {
            return IsZip(stream) ? ParseZip(stream) : ParseXml(stream);
        }
        catch (Exception ex)
        {
            return Error.Failure(
                code: "BookMetadata.Fb2ParseFailed",
                description: $"Failed to parse FB2 metadata: {ex.Message}");
        }
    }

    private static bool IsZip(Stream stream)
    {
        var position = stream.Position;
        try
        {
            var header = new byte[4];
            return stream.Read(header, 0, header.Length) == header.Length
                && header[0] == 'P'
                && header[1] == 'K'
                && header[2] is 3 or 5;
        }
        finally
        {
            stream.Position = position;
        }
    }

    private static ErrorOr<BookMetadata> ParseZip(Stream stream)
    {
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);

        var entry = archive.Entries.FirstOrDefault(e =>
            e.FullName.EndsWith(".fb2", StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return Error.NotFound(
                code: "BookMetadata.Fb2EntryNotFound",
                description: "No .fb2 entry found in archive.");
        }

        using var entryStream = entry.Open();
        return ParseXml(entryStream);
    }

    private static ErrorOr<BookMetadata> ParseXml(Stream stream)
    {
        var result = new BookMetadata();
        var doc = XDocument.Load(stream);
        var root = doc.Root;
        var ns = root?.GetDefaultNamespace() ?? XNamespace.None;

        var description = root?.Element(ns + "description");
        var titleInfo = description?.Element(ns + "title-info");

        if (titleInfo is not null)
        {
            result.Title = titleInfo.Element(ns + "book-title")?.Value ?? string.Empty;

            foreach (var authorElement in titleInfo.Elements(ns + "author"))
            {
                var first = authorElement.Element(ns + "first-name")?.Value ?? string.Empty;
                var last = authorElement.Element(ns + "last-name")?.Value ?? string.Empty;
                var full = string.Join(" ", new[] { first, last }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (!string.IsNullOrWhiteSpace(full)) result.Authors.Add(full);
            }
        }

        result.Isbn = description?.Element(ns + "publish-info")?.Element(ns + "isbn")?.Value ?? string.Empty;

        SetCover(result, root, ns, titleInfo);

        return result;
    }

    private static void SetCover(BookMetadata result, XElement? root, XNamespace ns, XElement? titleInfo)
    {
        var coverBytes = ExtractCoverBytes(root, ns, titleInfo);
        var coverMime = CoverMimeDetector.Detect(coverBytes);

        if (coverMime is null)
        {
            coverBytes = PlaceholderCoverGenerator.Create(result.Title, result.Author);
            coverMime = CoverMimeDetector.Jpeg;
        }

        result.CoverImage = coverBytes;
        result.CoverContentType = coverMime;
    }

    private static byte[]? ExtractCoverBytes(XElement? root, XNamespace ns, XElement? titleInfo)
    {
        var href = titleInfo?.Element(ns + "coverpage")?
            .Elements(ns + "image")
            .Select(e => e.Attributes()
                .FirstOrDefault(a => a.Name.LocalName == "href")?
                .Value)
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        if (href is null)
        {
            return null;
        }

        var id = href.TrimStart('#');

        var binary = root?.Elements(ns + "binary")
            .FirstOrDefault(b => string.Equals((string?)b.Attribute("id"), id, StringComparison.Ordinal))
            ?? root?.Elements(ns + "binary")
                .FirstOrDefault(b => string.Equals((string?)b.Attribute("id"), id, StringComparison.OrdinalIgnoreCase));

        if (binary is null || string.IsNullOrWhiteSpace(binary.Value))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(binary.Value);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}