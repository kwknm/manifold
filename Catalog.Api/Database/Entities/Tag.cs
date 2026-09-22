using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.Database.Entities;

public class Tag
{
    public Guid Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    [MaxLength(7)] public string ColorHex { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    
    public ICollection<Book> Books { get; set; } = [];
}