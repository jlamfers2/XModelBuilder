namespace XModelBuilder.Demo.Shop.Services;

/// <summary>Base type for expected domain failures that map onto HTTP status codes.</summary>
public abstract class DomainException(string message) : Exception(message);

/// <summary>Requested resource does not exist → 404.</summary>
public sealed class NotFoundException(string message) : DomainException(message);

/// <summary>Caller is authenticated but not allowed to touch this resource → 403.</summary>
public sealed class ForbiddenException(string message) : DomainException(message);

/// <summary>A business rule was violated (e.g. insufficient stock, order not payable) → 409.</summary>
public sealed class BusinessRuleException(string message) : DomainException(message);
