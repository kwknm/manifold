using System.Text;

namespace BookMetadata.Tests.Unit.TestData;

public static class PdfBuilder
{
    public static Stream Create(string? title = null, string? author = null)
    {
        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");

        var offsets = new List<long>();

        void AddObject(string content)
        {
            offsets.Add(sb.Length);
            sb.Append(content).Append("endobj\n");
        }

        AddObject("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\n");
        AddObject("2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\n");
        AddObject("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> >>\n");

        var info = "<< ";
        if (title is not null) info += $"/Title ({Escape(title)}) ";
        if (author is not null) info += $"/Author ({Escape(author)}) ";
        info += ">>";
        AddObject($"4 0 obj\n{info}\n");

        var xrefOffset = sb.Length;
        sb.Append("xref\n");
        sb.Append($"0 {offsets.Count + 1}\n");
        sb.Append("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append($"{offset:0000000000} 00000 n \n");
        }

        sb.Append("trailer\n");
        sb.Append($"<< /Size {offsets.Count + 1} /Root 1 0 R /Info 4 0 R >>\n");
        sb.Append("startxref\n");
        sb.Append($"{xrefOffset}\n");
        sb.Append("%%EOF");

        var ms = new MemoryStream(Encoding.Latin1.GetBytes(sb.ToString()));
        ms.Position = 0;
        return ms;
    }

    private static string Escape(string value)
        => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
}