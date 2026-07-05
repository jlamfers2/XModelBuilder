namespace XModelBuilder.Demo.Shop.Contracts;

/// <summary>The shipping/billing address on a <see cref="PlaceOrderRequest"/>.</summary>
public class AddressRequest
{
    public string Street { get; set; } = null!;
    public string HouseNumber { get; set; } = null!;
    public string PostalCode { get; set; } = null!;
    public string City { get; set; } = null!;
    public string Country { get; set; } = "NL";
}
