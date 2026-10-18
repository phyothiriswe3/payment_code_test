namespace Checkout.Application.Checkout;

/// <summary>Idempotency tuning for <see cref="CheckoutService"/>, bound from the "App" configuration section.</summary>
public sealed class CheckoutOptions
{
    public const string SectionName = "App";

    /// <summary>Redis lock TTL. Lets a crashed owner's lock expire instead of blocking the key forever.</summary>
    public int CheckoutLockTtlSeconds { get; set; } = 30;

    /// <summary>How long a duplicate request waits for the first request to settle before answering 409.</summary>
    public int CheckoutWaitSeconds { get; set; } = 10;

    /// <summary>Database polling interval while waiting for the first request to settle.</summary>
    public int WaitPollMilliseconds { get; set; } = 200;
}
