namespace Catalog.Api.Clients;

public interface IFilesClient
{
    Task<Guid> UploadBookAsync(IFormFile file, CancellationToken ct = default);
}
