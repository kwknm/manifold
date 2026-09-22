using Microsoft.AspNetCore.Http;

namespace Shared.Clients;

public interface IFilesClient
{
    Task<Guid> UploadBookAsync(IFormFile file, CancellationToken ct = default);
}
