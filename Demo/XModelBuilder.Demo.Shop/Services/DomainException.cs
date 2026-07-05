namespace XModelBuilder.Demo.Shop.Services;

/// <summary>Base type for expected domain failures that map onto HTTP status codes.</summary>
public abstract class DomainException(string message) : Exception(message);
