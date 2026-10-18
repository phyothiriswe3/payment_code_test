using Checkout.Application.Errors;
using Checkout.Infrastructure.Payments.TwoC2P;
using FluentAssertions;
using Microsoft.IdentityModel.Tokens;

namespace Checkout.UnitTests.TwoC2P;

public class TwoC2PJwtTests
{
    private const string Secret = "unit-test-secret-not-a-real-key";

    [Fact]
    public void Sign_then_Verify_round_trips_payload()
    {
        var jwt = new TwoC2PJwt(Secret);

        var token = jwt.Sign(new { merchantID = "JT01", invoiceNo = "INV1", amount = 25.00m });
        var payload = jwt.Verify(token);

        token.Split('.').Should().HaveCount(3);
        payload.GetProperty("merchantID").GetString().Should().Be("JT01");
        payload.GetProperty("invoiceNo").GetString().Should().Be("INV1");
        payload.GetProperty("amount").GetDecimal().Should().Be(25.00m);
    }

    [Fact]
    public void Sign_uses_HS256_header()
    {
        var token = new TwoC2PJwt(Secret).Sign(new { a = 1 });

        Base64UrlEncoder.Decode(token.Split('.')[0]).Should().Be("""{"alg":"HS256","typ":"JWT"}""");
    }

    [Fact]
    public void Verify_throws_when_payload_is_tampered()
    {
        var jwt = new TwoC2PJwt(Secret);
        var parts = jwt.Sign(new { invoiceNo = "INV1", amount = 25.00m }).Split('.');
        var forged = Base64UrlEncoder.Encode("""{"invoiceNo":"INV1","amount":0.01}""");

        var act = () => jwt.Verify($"{parts[0]}.{forged}.{parts[2]}");

        act.Should().Throw<InvalidSignatureException>();
    }

    [Fact]
    public void Verify_throws_when_signed_with_another_key()
    {
        var token = new TwoC2PJwt("some-other-secret").Sign(new { invoiceNo = "INV1" });

        var act = () => new TwoC2PJwt(Secret).Verify(token);

        act.Should().Throw<InvalidSignatureException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    [InlineData("a.b.c")]
    public void Verify_throws_on_malformed_input(string token)
    {
        var act = () => new TwoC2PJwt(Secret).Verify(token);

        act.Should().Throw<InvalidSignatureException>();
    }
}
