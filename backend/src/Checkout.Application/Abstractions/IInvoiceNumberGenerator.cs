namespace Checkout.Application.Abstractions;

public interface IInvoiceNumberGenerator
{
    string Next();
}
