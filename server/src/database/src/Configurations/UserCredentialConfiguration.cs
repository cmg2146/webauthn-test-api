namespace WebAuthnTest.Database;

using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.EntityFrameworkCore.ChangeTracking;

public class UserCredentialConfiguration : EntityConfiguration<UserCredential>
{
    public override void Configure(EntityTypeBuilder<UserCredential> builder)
    {
        base.Configure(builder);

        builder.ToTable("UserCredential");
        
        //Configure a shadow property to hold the hash of the Credential Id. We
        //need the Credential Id to be unique but it's too big to index, so we
        //hash it and index the hash. We dont need to query the hash.
        builder
            .Property<byte[]>("CredentialIdHash")
            .HasMaxLength(64)
            .HasValueGenerator<CredentialIdHashGenerator>();

        builder.HasAlternateKey("CredentialIdHash");

        builder
            .Property(t => t.DisplayName)
            .HasMaxLength(255);

        //https://www.w3.org/TR/webauthn/#attestation-statement-format-identifier
        builder
            .Property(t => t.AttestationFormatId)
            .HasMaxLength(32);
    }

    public class CredentialIdHashGenerator : ValueGenerator<byte[]>
    {
        public override byte[] Next(EntityEntry entry)
        {
            using (var hash = SHA512.Create())
            {
                var typedEntry = entry as EntityEntry<UserCredential>;
                return hash.ComputeHash(typedEntry!.Entity.CredentialId);
            }
        }

        public override bool GeneratesTemporaryValues { get; } = false;
        public override bool GeneratesStableValues { get; } = true;
    } 
}