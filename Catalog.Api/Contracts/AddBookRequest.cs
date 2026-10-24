namespace Catalog.Api.Contracts;

public sealed class AddBookRequest
{
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Comma-separated list of tag identifiers. Repeated form fields are also supported.
    /// </summary>
    public string? TagIds { get; set; }
}
