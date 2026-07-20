namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>
/// A customer/user of the shop. <see cref="Email"/> is the natural key used for authentication and
/// look-ups. The <see cref="Addresses"/> and <see cref="PaymentMethods"/> collections make this the
/// root of a non-trivial object graph.
/// </summary>
public class Customer : IAuditable
{
    public int Id { get; set; }
    public string Email { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<Address> Addresses { get; set; } = [];
    public List<PaymentMethod> PaymentMethods { get; set; } = [];
    public List<Order> Orders { get; set; } = [];
}
