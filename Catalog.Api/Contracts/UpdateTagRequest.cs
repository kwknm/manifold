namespace Catalog.Api.Contracts;

public sealed record UpdateTagRequest(
    string Name,
    string ColorHex);
