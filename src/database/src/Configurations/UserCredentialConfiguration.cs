namespace WebAuthnTest.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class UserCredentialConfiguration : EntityConfiguration<UserCredential>
{
    public override void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        base.Configure(builder);

        builder.ToTable("UserCredential");
        
        builder.HasAlternateKey(t => t.CredentialIdHash);

        builder
            .Property(t => t.CredentialIdHash)
            .HasMaxLength(64);

        builder
            .Property(t => t.DisplayName)
            .HasMaxLength(255);

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