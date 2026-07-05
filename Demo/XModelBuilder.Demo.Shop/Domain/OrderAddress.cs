using Microsoft.EntityFrameworkCore;

namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>An owned value object: the address as captured on the order (not a customer address row).</summary>
[Owned]
public class OrderAddress
{
    public string Street { get; set; } = null!;
    public string HouseNumber { get; set; } = null!;
    public string PostalCode { get; set; } = null!;
    public string City { get; set; } = null!;
    public string Country { get; set; } = "NL";
}
