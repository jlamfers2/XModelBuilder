using XModelBuilder.Demo.Shop.Domain;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Common;

/// <summary>Maps the Dutch role words used in scenarios to the application's role names.</summary>
internal static class RoleMap
{
    public static string ToRole(string dutch) => dutch switch
    {
        "klant" => Roles.Customer,
        "magazijnmedewerker" => Roles.WarehouseOperator,
        "beheerder" => Roles.Admin,
        _ => throw new ArgumentException($"Onbekende rol '{dutch}'."),
    };
}
