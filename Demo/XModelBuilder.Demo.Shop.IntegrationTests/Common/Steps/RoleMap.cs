using XModelBuilder.Demo.Shop.Domain;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Common;

/// <summary>Maps the role words used in scenarios to the application's role names.</summary>
internal static class RoleMap
{
    public static string ToRole(string role) => role switch
    {
        "customer" => Roles.Customer,
        "warehouse operator" => Roles.WarehouseOperator,
        "admin" => Roles.Admin,
        _ => throw new ArgumentException($"Unknown role '{role}'."),
    };
}
