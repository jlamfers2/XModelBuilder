namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>Role name constants used in <c>[Authorize(Roles = ...)]</c> and in claims.</summary>
public static class Roles
{
    public const string Customer = nameof(UserRole.Customer);
    public const string WarehouseOperator = nameof(UserRole.WarehouseOperator);
    public const string Admin = nameof(UserRole.Admin);
}
