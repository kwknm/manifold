using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Database.Entities;

[Index(nameof(UserId), nameof(FullName), IsUnique = true)]
public class Author
{
    public Guid Id { get; set; }
    [MaxLength(64)] public string FullName { get; set; } = string.Empty;
    public Guid UserId { get; set; }

    public ICollection<Book> Books { get; set; } = [];
}