using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Database.Entities;

[Index(nameof(UserId))]
public class Book
{
    public Guid Id { get; set; }
    [MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(20)] public string? Isbn { get; set; } = string.Empty;
    public Guid FileId { get; set; }
    public Guid? CoverFileId { get; set; }
    public Guid UserId { get; set; }
    
    public ICollection<Tag> Tags { get; set; } = [];
    public ICollection<Author> Authors { get; set; } = [];
}