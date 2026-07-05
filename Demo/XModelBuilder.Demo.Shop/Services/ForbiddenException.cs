namespace XModelBuilder.Demo.Shop.Services;

/// <summary>Caller is authenticated but not allowed to touch this resource → 403.</summary>
public sealed class ForbiddenException(string message) : DomainException(message);
