using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Database.Entities;

[Index(nameof(Email), IsUnique = true)]
public class User
{
    public Guid Id { get; set; }
    [MaxLength(254)] public string Email { get; set; } = string.Empty;
    [MaxLength(12)] public string UserName { get; set; } = string.Empty;
    [MaxLength(256)] public string PasswordHash { get; set; } = string.Empty;

    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}