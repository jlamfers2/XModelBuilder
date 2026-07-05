namespace XModelBuilder.Demo.Shop.Services;

/// <summary>A business rule was violated (e.g. insufficient stock, order not payable) → 409.</summary>
public sealed class BusinessRuleException(string message) : DomainException(message);
