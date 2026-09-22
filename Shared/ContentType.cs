namespace Shared
{
    public enum ContentType
    {
        [Metadata(Value = "application/epub+zip")]
        EPUB,

        [Metadata(Value = "application/pdf")]
        PDF,

        [Metadata(Value = "application/x-mobipocket-ebook")]
        MOBI,

        [Metadata(Value = "application/vnd.amazon.ebook")]
        AZW,

        [Metadata(Value = "application/x-fictionbook+xml")]
        FB2,

        [Metadata(Value = "application/x-cbr")]
        CBZ,

        [Metadata(Value = "application/x-cbr")]
        CBR,

        [Metadata(Value = "application/x-dtbook+xml")]
        DTB,

        [Metadata(Value = "text/plain")]
        TXT,

        [Metadata(Value = "text/plain")]
        TEXT,

        [Metadata(Value = "text/plain")]
        ASC,

        [Metadata(Value = "application/rtf")]
        RTF,

        [Metadata(Value = "application/x-abiword")]
        ABW,

        [Metadata(Value = "application/msword")]
        DOC,

        [Metadata(Value = "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
        DOCX,

        [Metadata(Value = "application/vnd.oasis.opendocument.text")]
        ODT,

        [Metadata(Value = "audio/mpeg")]
        MP3,

        [Metadata(Value = "audio/mpeg")]
        MPGA,

        [Metadata(Value = "audio/mp4a-latm")]
        M4A,

        [Metadata(Value = "audio/mp4a-latm")]
        M4B,

        [Metadata(Value = "audio/ogg")]
        OGA,

        [Metadata(Value = "audio/x-wav")]
        WAV,

        [Metadata(Value = "audio/flac")]
        FLAC,

        [Metadata(Value = "audio/x-matroska")]
        MKA,

        [Metadata(Value = "application/atom+xml")]
        ATOM,

        [Metadata(Value = "application/rss+xml")]
        RSS,

        [Metadata(Value = "application/marc")]
        MRC,

        [Metadata(Value = "application/marcxml+xml")]
        MRCX,

        [Metadata(Value = "application/zip")]
        ZIP,

        [Metadata(Value = "application/x-rar-compressed")]
        RAR,

        [Metadata(Value = "application/x-tar")]
        TAR,

        [Metadata(Value = "application/gzip")]
        GZ,

        [Metadata(Value = "application/x-bzip2")]
        BZ2,

        [Metadata(Value = "image/jpeg")]
        JPG,

        [Metadata(Value = "image/jpeg")]
        JPEG,

        [Metadata(Value = "image/png")]
        PNG,

        [Metadata(Value = "image/gif")]
        GIF,

        [Metadata(Value = "image/webp")]
        WEBP,

        [Metadata(Value = "image/svg+xml")]
        SVG,

        [Metadata(Value = "image/tiff")]
        TIFF,

        [Metadata(Value = "image/vnd.djvu")]
        DJVU,

        [Metadata(Value = "text/html")]
        HTML,

        [Metadata(Value = "text/html")]
        HTM,

        [Metadata(Value = "text/markdown")]
        MD,

        [Metadata(Value = "application/octet-stream")]
        DEFAULT
    }

    [AttributeUsage(AttributeTargets.Field)]
    internal class Metadata : Attribute
    {
        public string Value { get; set; } = "text/plain";
    }

    public static class ContentTypeExtensions
    {
        private static object? GetMetadata(ContentType ct)
        {
            var type = ct.GetType();
            var info = type.GetMember(ct.ToString());
            if (info != null && info.Length > 0)
            {
                var attrs = info[0].GetCustomAttributes(typeof(Metadata), false);
                if (attrs != null && attrs.Length > 0)
                {
                    return attrs[0];
                }
            }
            return null;
        }

        public static string ToValue(this ContentType ct)
        {
            var metadata = GetMetadata(ct);
            return metadata != null ? ((Metadata)metadata).Value : ct.ToString();
        }
    }
}