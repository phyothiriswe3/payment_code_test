using Checkout.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Checkout.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("customer");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.FirstName).HasColumnName("first_name").HasMaxLength(100).IsRequired();
        b.Property(x => x.LastName).HasColumnName("last_name").HasMaxLength(100).IsRequired();
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(255).IsRequired();

        b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("ux_customer_email");

        b.HasMany(x => x.Cards)
            .WithOne(x => x.Customer)
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_customer_card_customer");
    }
}
