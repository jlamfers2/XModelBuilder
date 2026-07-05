namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>
/// A customer/user of the shop. <see cref="Email"/> is the natural key used for authentication and
/// look-ups. The <see cref="Addresses"/> and <see cref="PaymentMethods"/> collections make this the
/// root of a non-trivial object graph.
/// </summary>
public class Customer
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public UserRole Role { get; set; }

    public List<Address> Addresses { get; set; } = [];
    public List<PaymentMethod> PaymentMethods { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
}

/// <summary>An address belonging to a <see cref="Customer"/> (their address book).</summary>
public class Address
{
    public int Id { get; set; }
    public AddressKind Kind { get; set; }
    public string Street { get; set; } = null!;
    public string HouseNumber { get; set; } = null!;
    public string PostalCode { get; set; } = null!;
    public string City { get; set; } = null!;
    public string Country { get; set; } = "NL";

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
}

/// <summary>A stored payment method belonging to a <see cref="Customer"/>.</summary>
public class PaymentMethod
{
    public int Id { get; set; }
    public PaymentMethodType Type { get; set; }
    public string Display { get; set; } = null!;

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
}
