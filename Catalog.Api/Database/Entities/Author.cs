using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.Database.Entities;

public class Author
{
    public Guid Id { get; set; }
    [MaxLength(64)] public string FullName { get; set; } = string.Empty;
    public Guid UserId { get; set; }

    public ICollection<Book> Books { get; set; } = [];
}