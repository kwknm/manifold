using Microsoft.AspNetCore.Http;

namespace Shared.Clients;

public interface IFilesClient
{
    Task<Guid> UploadBookAsync(IFormFile file, CancellationToken ct = default);
    Task<DownloadedFile> DownloadFileAsync(Guid fileId, CancellationToken ct = default);
    Task DeleteFileAsync(Guid fileId, CancellationToken ct = default);
}
