using System.ComponentModel.DataAnnotations;

namespace Checkout.Infrastructure.Payments.TwoC2P;

/// <summary>Bound from configuration section <c>TwoC2P</c>. SecretKey comes from env/.env only, never committed.</summary>
public sealed class TwoC2POptions
{
    public const string SectionName = "TwoC2P";

    [Required] public string MerchantId { get; set; } = string.Empty;

    /// <summary>Merchant secret key used for HS256 JWT signing/verification.</summary>
    [Required] public string SecretKey { get; set; } = string.Empty;

    /// <summary>e.g. https://sandbox-pgw.2c2p.com/payment/4.3/ (trailing slash required for relative paths).</summary>
    [Required, Url] public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Server-to-server notification target (receives <c>{payload}</c>).</summary>
    [Required, Url] public string BackendReturnUrl { get; set; } = string.Empty;

    /// <summary>Browser return base; the provider appends <c>?invoiceNo={invoiceNo}</c>.</summary>
    [Required, Url] public string FrontendReturnUrl { get; set; } = string.Empty;
}
