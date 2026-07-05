namespace XModelBuilder.Demo.Shop.Domain;

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
