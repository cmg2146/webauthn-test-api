namespace WebAuthnTest.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;

public class WebAuthnTestDbContext : DbContext, IDataProtectionKeyContext
{
    public WebAuthnTestDbContext(DbContextOptions<WebAuthnTestDbContext> options)
        : base(options)
    {

    }

    public DbSet<User> Users { get; set; } = default!;
    public DbSet<UserCredential> UserCredentials { get; set; } = default!;
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new UserCredentialConfiguration());
    }
}