using Checkout.Application.Abstractions;

namespace Checkout.Infrastructure.Common;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
