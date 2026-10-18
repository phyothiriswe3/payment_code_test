using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Checkout.Application.Errors;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Checkout.Infrastructure.Payments.TwoC2P;

/// <summary>
/// HS256 JWT signer/verifier for 2C2P payloads (https://developer.2c2p.com/docs/json-web-tokens-jwt):
/// header <c>{"alg":"HS256","typ":"JWT"}</c>, key = merchant secret key as UTF-8 bytes.
/// Only the signature is validated (2C2P tokens carry no iss/aud/exp).
/// HMAC is computed directly rather than via JsonWebTokenHandler because IdentityModel 8.x
/// rejects HMAC keys shorter than 256 bits, while 2C2P merchant keys are not guaranteed to meet that.
/// </summary>
public sealed class TwoC2PJwt
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    private static readonly string EncodedHeader =
        Base64UrlEncoder.Encode("""{"alg":"HS256","typ":"JWT"}""");

    private readonly byte[] _key;

    public TwoC2PJwt(string secretKey)
    {
        if (string.IsNullOrEmpty(secretKey))
            throw new ArgumentException("2C2P secret key is not configured.", nameof(secretKey));
        _key = Encoding.UTF8.GetBytes(secretKey);
    }

    public TwoC2PJwt(IOptions<TwoC2POptions> options) : this(options.Value.SecretKey) { }

    public string Sign(object payload)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, payload.GetType(), JsonOptions);
        var signingInput = EncodedHeader + "." + Base64UrlEncoder.Encode(json);
        var signature = HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(signingInput));
        return signingInput + "." + Base64UrlEncoder.Encode(signature);
    }

    /// <summary>Verifies the HS256 signature and returns the decoded payload. Throws <see cref="InvalidSignatureException"/>.</summary>
    public JsonElement Verify(string jwt)
    {
        if (string.IsNullOrWhiteSpace(jwt))
            throw new InvalidSignatureException("Missing 2C2P payload.");

        var parts = jwt.Split('.');
        if (parts.Length != 3)
            throw new InvalidSignatureException("Malformed 2C2P payload.");

        try
        {
            using (var header = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(parts[0])))
            {
                if (!header.RootElement.TryGetProperty("alg", out var alg) || alg.GetString() != "HS256")
                    throw new InvalidSignatureException("Unsupported 2C2P payload algorithm.");
            }

            var expected = HMACSHA256.HashData(_key, Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]));
            var actual = Base64UrlEncoder.DecodeBytes(parts[2]);
            if (!CryptographicOperations.FixedTimeEquals(expected, actual))
                throw new InvalidSignatureException("Invalid 2C2P payload signature.");

            using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(parts[1]));
            return payload.RootElement.Clone();
        }
        catch (InvalidSignatureException)
        {
            throw;
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            throw new InvalidSignatureException("Malformed 2C2P payload.");
        }
    }
}
