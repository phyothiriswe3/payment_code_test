using System.Security.Cryptography;
using Checkout.Application.Abstractions;

namespace Checkout.Infrastructure.Common;

public sealed class InvoiceNumberGenerator(IClock clock) : IInvoiceNumberGenerator
{
    // "INV" + yyyyMMddHHmmss + 6 random digits = 23 alphanumeric chars.
    public string Next() =>
        $"INV{clock.UtcNow:yyyyMMddHHmmss}{RandomNumberGenerator.GetInt32(0, 1_000_000):D6}";
}
