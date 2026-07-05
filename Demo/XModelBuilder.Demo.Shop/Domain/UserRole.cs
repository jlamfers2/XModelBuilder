namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>The application roles a user can have. <see cref="Guest"/> means "not authenticated".</summary>
public enum UserRole
{
    Guest,
    Customer,
    WarehouseOperator,
    Admin,
}
