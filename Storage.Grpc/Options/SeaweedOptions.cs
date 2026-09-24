namespace Storage.Grpc.Options;

public sealed class SeaweedOptions
{
    public const string SectionName = "Seaweed";
    public string BooksBucketName { get; set; } = "books";
    public string CoversBucketName { get; set; } = "covers";
    public int PresignedUrlExpirySeconds { get; set; } = 600;
}
