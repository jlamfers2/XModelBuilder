namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>The application roles a user can have. <see cref="Guest"/> means "not authenticated".</summary>
public enum UserRole
{
    Guest,
    Customer,
    WarehouseOperator,
    Admin,
}

public enum AddressKind
{
    Billing,
    Shipping,
}

public enum PaymentMethodType
{
    CreditCard,
    Ideal,
    Invoice,
}

public enum PaymentStatus
{
    Pending,
    Captured,
    Failed,
}

public enum OrderStatus
{
    Pending,
    Paid,
    Shipped,
    Cancelled,
}
