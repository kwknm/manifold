using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Auth.Api.Database.Entities;

namespace Auth.Api.Database.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasOne(t => t.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Rotating a token writes ReplacedByTokenHash before the replacement row is
        // inserted, so a duplicate hash would corrupt the rotation chain. The index is
        // filtered because replaced/revoked rows legitimately keep a null in this column.
        builder.HasIndex(t => t.ReplacedByTokenHash)
            .IsUnique()
            .HasFilter("\"ReplacedByTokenHash\" IS NOT NULL");
    }
}
