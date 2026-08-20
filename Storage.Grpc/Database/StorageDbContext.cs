using Microsoft.EntityFrameworkCore;
using Storage.Grpc.Database.Entities;

namespace Storage.Grpc.Database;

public class StorageDbContext(DbContextOptions<StorageDbContext> options) : DbContext(options)
{
    public DbSet<StorageFile> Files => Set<StorageFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(StorageDbContext).Assembly);
    }

}