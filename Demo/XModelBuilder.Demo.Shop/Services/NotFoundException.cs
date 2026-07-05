namespace XModelBuilder.Demo.Shop.Services;

/// <summary>Requested resource does not exist → 404.</summary>
public sealed class NotFoundException(string message) : DomainException(message);
