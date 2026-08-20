using System.ComponentModel.DataAnnotations;

namespace Storage.Grpc.Database.Entities;

public class StorageFile
{
    public Guid Id { get; set; }
    [MaxLength(64)] public string BucketName { get; set; } = string.Empty;
    [MaxLength(128)] public string ObjectName { get; set; } = string.Empty;
    [MaxLength(128)] public string ContentType { get; set; } = string.Empty;
    [MaxLength(512)] public string OriginalFileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}