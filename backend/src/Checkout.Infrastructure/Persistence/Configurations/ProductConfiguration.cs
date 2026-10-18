using Checkout.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Checkout.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("product");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(500);
        b.Property(x => x.Price).HasColumnName("price").HasColumnType("decimal(12,2)");
        b.Property(x => x.Currency).HasColumnName("currency").HasColumnType("char(3)").IsRequired();
        b.Property(x => x.PaymentType)
            .HasColumnName("payment_type")
            .HasColumnType("enum('OneTime','Recurring')")
            .HasConversion<string>()
            .IsRequired();
    }
}
