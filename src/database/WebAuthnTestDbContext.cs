namespace WebAuthnTest.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

public class WebAuthnTestDbContext : DbContext, IDataProtectionKeyContext
{
    public WebAuthnTestDbContext(DbContextOptions<WebAuthnTestDbContext> options)
        : base(options)
    {
        ChangeTracker.StateChanged += UpdateTimestamps;
        ChangeTracker.Tracked += UpdateTimestamps;
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<UserCredential> UserCredentials { get; set; } = null!;
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;
    public DbSet<DistributedCacheEntry> DistributedCacheEntries { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new UserCredentialConfiguration());
    }

    private static void UpdateTimestamps(object? sender, EntityEntryEventArgs e)
    {
        if (e.Entry.Entity is Entity entity)
        {
            if (e.Entry.State == EntityState.Added)
            {
                entity.Created = DateTime.UtcNow;
            }
            else if (e.Entry.State == EntityState.Modified)
            {
                entity.Updated = DateTime.UtcNow;
            }
        }
    }
}
