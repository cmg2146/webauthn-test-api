namespace WebAuthnTest.Database;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.ValueGeneration;
using Microsoft.EntityFrameworkCore.ChangeTracking;

public class EntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity> where TEntity : Entity
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder
            .Property(t => t.Created)
            .ValueGeneratedOnAdd()
            .HasValueGenerator<UtcTimeRightNowGenerator>();

        builder
            .Property(t => t.Updated)
            .ValueGeneratedOnUpdate()
            .HasValueGenerator<UtcTimeRightNowGenerator>();
    }

    public class UtcTimeRightNowGenerator : ValueGenerator<DateTime>
    {
        public override DateTime Next(EntityEntry entry) => DateTime.UtcNow;
        public override bool GeneratesTemporaryValues { get; } = false;
    }    
}