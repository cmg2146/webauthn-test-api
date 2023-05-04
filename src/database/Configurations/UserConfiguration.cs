namespace WebAuthnTest.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class UserConfiguration : EntityConfiguration<User>
{
    public const int NAME_MAX_LENGTH = 255;
    public const int USER_HANDLE_MAX_LENGTH = 64;

    public override void Configure(EntityTypeBuilder<User> builder)
    {
        base.Configure(builder);

        builder.ToTable("User");

        builder
            .Property(t => t.DisplayName)
            .HasMaxLength(NAME_MAX_LENGTH);

        builder
            .Property(t => t.FirstName)
            .HasMaxLength(NAME_MAX_LENGTH);

        builder
            .Property(t => t.LastName)
            .HasMaxLength(NAME_MAX_LENGTH);

        builder
            .Property(t => t.UserHandle)
            .HasMaxLength(USER_HANDLE_MAX_LENGTH)
            .Metadata
            .SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder
            .HasIndex(t => t.UserHandle)
            .IsUnique();
    }
}
