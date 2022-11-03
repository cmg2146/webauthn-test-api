namespace WebAuthnTest.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class UserConfiguration : EntityConfiguration<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        base.Configure(builder);

        builder.ToTable("User");

        builder
            .Property(t => t.DisplayName)
            .HasMaxLength(255);

        builder
            .Property(t => t.FirstName)
            .HasMaxLength(255);

        builder
            .Property(t => t.LastName)
            .HasMaxLength(255);
    }
}