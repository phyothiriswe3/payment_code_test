using Checkout.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Checkout.Infrastructure.Persistence.Configurations;

internal sealed class PaymentTransactionConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b)
    {
        b.ToTable("transaction");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.CustomerId).HasColumnName("customer_id");
        b.Property(x => x.ProductId).HasColumnName("product_id");
        b.Property(x => x.CustomerCardId).HasColumnName("customer_card_id");

        b.Property(x => x.InvoiceNo).HasColumnName("invoice_no").HasMaxLength(50).IsRequired();
        b.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(100).IsRequired();
        // Hex-encoded SHA-256 of the request body (customerId|productId|cardId).
        b.Property(x => x.RequestFingerprint).HasColumnName("request_fingerprint").HasColumnType("char(64)").IsRequired();

        b.Property(x => x.Amount).HasColumnName("amount").HasColumnType("decimal(12,2)");
        b.Property(x => x.Currency).HasColumnName("currency").HasColumnType("char(3)").IsRequired();
        b.Property(x => x.Status)
            .HasColumnName("status")
            .HasColumnType("enum('Pending','Succeeded','Failed')")
            .HasConversion<string>()
            .IsRequired();
        b.Property(x => x.Flow)
            .HasColumnName("flow")
            .HasColumnType("enum('HostedPage','SavedCard')")
            .HasConversion<string>()
            .IsRequired();

        b.Property(x => x.PaymentToken).HasColumnName("payment_token").HasMaxLength(512);
        b.Property(x => x.RedirectUrl).HasColumnName("redirect_url").HasMaxLength(2048);

        b.Property(x => x.PaymentChannel).HasColumnName("payment_channel").HasMaxLength(20);
        b.Property(x => x.MaskedCardNo).HasColumnName("masked_card_no").HasMaxLength(30);
        b.Property(x => x.ProviderRef).HasColumnName("provider_ref").HasMaxLength(50);
        b.Property(x => x.RespCode).HasColumnName("resp_code").HasMaxLength(10);
        b.Property(x => x.RespDesc).HasColumnName("resp_desc").HasMaxLength(100);

        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("datetime(6)");
        b.Property(x => x.PaidAt).HasColumnName("paid_at").HasColumnType("datetime(6)");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime(6)");
        b.Property(x => x.ProcessedAt).HasColumnName("processed_at").HasColumnType("datetime(6)");

        b.HasIndex(x => x.InvoiceNo).IsUnique().HasDatabaseName("ux_transaction_invoice_no");
        b.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("ux_transaction_idempotency_key");
        b.HasIndex(x => x.CustomerId).HasDatabaseName("ix_transaction_customer_id");
        b.HasIndex(x => x.Status).HasDatabaseName("ix_transaction_status");
        b.HasIndex(x => x.ProductId).HasDatabaseName("ix_transaction_product_id");
        b.HasIndex(x => x.CustomerCardId).HasDatabaseName("ix_transaction_customer_card_id");

        b.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_transaction_customer");
        b.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_transaction_product");
        b.HasOne(x => x.CustomerCard)
            .WithMany()
            .HasForeignKey(x => x.CustomerCardId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_transaction_customer_card");
    }
}
