using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.Database.Entities;

public class Book
{
    public Guid Id { get; set; }
    [MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(100)] public string? Author { get; set; } = string.Empty;
    public int PageCount { get; set; }
    [MaxLength(20)] public string? Isbn { get; set; } = string.Empty;
    public Guid FileId { get; set; }
    public Guid? CoverFileId { get; set; }
    public Guid UserId { get; set; }
    
    public ICollection<Tag> Tags { get; set; } = [];
}