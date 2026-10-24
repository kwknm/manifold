using Catalog.Api.Database.Entities;

namespace Catalog.Api.Contracts;

public sealed class BookResponse
{
    public BookResponse()
    {
    }

    public BookResponse(Book book)
    {
        Id = book.Id;
        Title = book.Title;
        Authors = [.. book.Authors.Select(a => a.FullName)];
        Isbn = book.Isbn;
        Tags = [.. book.Tags.Select(t => new TagResponse(t))];
        UserId = book.UserId;
    }

    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public IList<string> Authors { get; set; } = [];
    public string? Isbn { get; set; }
    public string? CoverUrl { get; set; }
    public IList<TagResponse> Tags { get; set; } = [];
    public Guid UserId { get; set; }
}
