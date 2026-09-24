namespace Shared.Clients;

public sealed record DownloadedFile(Stream Content, string FileName, string ContentType, long SizeBytes);
