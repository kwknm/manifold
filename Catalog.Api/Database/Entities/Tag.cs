using System.ComponentModel.DataAnnotations;

namespace Catalog.Api.Database.Entities;

public class Tag
{
    public Guid Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = string.Empty;
    public Book Book { get; set; } = null!;
}