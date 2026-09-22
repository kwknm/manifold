namespace Storage.Grpc.Options;

public sealed class MinioOptions
{
    public const string SectionName = "Minio";
    public string Endpoint { get; set; } = "localhost:9000";
    public string AccessKey { get; set; } = "minioadmin";
    public string SecretKey { get; set; } = "minioadmin";
    public bool Secure { get; set; }
    public string BucketName { get; set; } = "files";
    public string CoversBucketName { get; set; } = "covers";
    public int PresignedUrlExpirySeconds { get; set; } = 600;
}
