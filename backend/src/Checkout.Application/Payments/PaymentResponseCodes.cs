namespace Checkout.Application.Payments;

/// <summary>2C2P response codes shared by the checkout flows and result handling.</summary>
public static class PaymentResponseCodes
{
    public const string Success = "0000";
    public const string RedirectRequired = "1001";
    public const string InquireForResult = "2000";

    // Codes that mean "no final outcome yet" (pending / in progress / not found yet / transient error).
    // Marking these Failed would be permanent and would drop a later 0000 for a charged customer.
    private static readonly HashSet<string> NonFinalCodes = new(StringComparer.Ordinal)
    {
        "0001", // Transaction is pending
        "0999", // System error
        RedirectRequired, // Redirect required (3DS)
        InquireForResult, // Transaction completed, inquire for result
        "2001", // Transaction in progress
        "2002", // Transaction not found
        "4009", // Request in progress
        "4071", // Inquiry record not exist
        "5002", // Timeout
        "5998", // Internal error
    };

    public static bool IsNonFinal(string? respCode) => respCode is not null && NonFinalCodes.Contains(respCode);
}
