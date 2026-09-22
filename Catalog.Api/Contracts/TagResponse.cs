using Catalog.Api.Database.Entities;

namespace Catalog.Api.Contracts;

public sealed class TagResponse
{
    public TagResponse()
    {
    }

    public TagResponse(Tag tag)
    {
        Id = tag.Id;
        Name = tag.Name;
        ColorHex = tag.ColorHex;
    }

    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = string.Empty;
};