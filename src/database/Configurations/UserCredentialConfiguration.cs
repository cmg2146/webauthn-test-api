namespace WebAuthnTest.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class UserCredentialConfiguration : EntityConfiguration<UserCredential>
{
    /// <summary>
    /// SHA 512 hash is 64 bytes
    /// </summary>
    public const int CREDENTIAL_HASH_MAX_LENGTH = 64;
    public const int DISPLAY_NAME_MAX_LENGTH = 255;

    public override void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        base.Configure(builder);

        builder.ToTable("UserCredential");

        builder.HasAlternateKey(t => t.CredentialIdHash);

        builder
            .Property(t => t.CredentialIdHash)
            .HasMaxLength(CREDENTIAL_HASH_MAX_LENGTH);

        builder
            .Property(t => t.DisplayName)
            .HasMaxLength(DISPLAY_NAME_MAX_LENGTH);

        builder
            .Property(t => t.CredentialId)
            .Metadata
            .SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder
            .Property(t => t.PublicKey)
            .Metadata
            .SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        builder
            .Property(t => t.AaGuid)
            .Metadata
            .SetAfterSaveBehavior(PropertySaveBehavior.Ignore);

        //https://www.w3.org/TR/webauthn/#attestation-statement-format-identifier
        builder
            .Property(t => t.AttestationFormatId)
            .HasMaxLength(32)
            .Metadata
            .SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
    }
}
