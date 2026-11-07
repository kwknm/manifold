using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Api.Database.Entities;

/// <summary>
/// <see cref="NormalizedName"/> is the identity of an author within a user's catalog: it is
/// <see cref="FullName"/> folded to a single case, so "Толстой" and "толстой" cannot become two
/// rows. The unique index lives on the key, not on the display name.
/// </summary>
[Index(nameof(UserId), nameof(NormalizedName), IsUnique = true)]
public class Author
{
    public Guid Id { get; set; }
    [MaxLength(64)] public string FullName { get; set; } = string.Empty;
    [MaxLength(64)] public string NormalizedName { get; set; } = string.Empty;
    public Guid UserId { get; set; }

    public ICollection<Book> Books { get; set; } = [];
}
