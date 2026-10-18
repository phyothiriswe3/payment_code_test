namespace Checkout.Application.Errors;

public abstract class AppException(string message) : Exception(message);

/// <summary>404</summary>
public sealed class NotFoundException(string message) : AppException(message);

/// <summary>400</summary>
public sealed class ValidationException(string message) : AppException(message);

/// <summary>409: a request with the same Idempotency-Key is still in progress.</summary>
public sealed class ConflictException(string message) : AppException(message);

/// <summary>422: Idempotency-Key reused with a different request body.</summary>
public sealed class IdempotencyMismatchException(string message) : AppException(message);

/// <summary>400: provider payload failed signature verification.</summary>
public sealed class InvalidSignatureException(string message) : AppException(message);
