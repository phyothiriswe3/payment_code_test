using Checkout.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Checkout.Infrastructure.Persistence.Configurations;

internal sealed class CustomerCardConfiguration : IEntityTypeConfiguration<CustomerCard>
{
    public void Configure(EntityTypeBuilder<CustomerCard> b)
    {
        b.ToTable("customer_card");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.Property(x => x.CustomerToken).HasColumnName("customer_token").HasMaxLength(100).IsRequired();
        b.Property(x => x.MaskedCardNo).HasColumnName("masked_card_no").HasMaxLength(30).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");

        // Relationship to customer is configured in CustomerConfiguration.
        b.HasIndex(x => new { x.CustomerId, x.CustomerToken })
            .IsUnique()
            .HasDatabaseName("ux_customer_card_customer_token");
    }
}
