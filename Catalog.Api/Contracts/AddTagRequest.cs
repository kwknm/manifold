namespace Catalog.Api.Contracts;

public sealed record AddTagRequest(
    string Name,
    string ColorHex);