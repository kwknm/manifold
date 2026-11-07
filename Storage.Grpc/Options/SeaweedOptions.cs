using System.ComponentModel.DataAnnotations;

namespace Storage.Grpc.Options;

public sealed class SeaweedOptions
{
    public const string SectionName = "Seaweed";

    [Required]
    [MaxLength(64)]
    public string BooksBucketName { get; set; } = "books";

    [Required]
    [MaxLength(64)]
    public string CoversBucketName { get; set; } = "covers";

    [Range(60, 86400)]
    public int PresignedUrlExpirySeconds { get; set; } = 600;

    /// <summary>
    /// Use HTTPS in generated presigned URLs. Keep <c>false</c> only for local
    /// SeaweedFS without TLS; production deployments must enable it.
    /// </summary>
    public bool PresignedUrlUseHttps { get; set; }
}
