using Microsoft.EntityFrameworkCore;
using XModelBuilder.Demo.Shop.Domain;

namespace XModelBuilder.Demo.Shop.Data;

public class ShopDbContext(DbContextOptions<ShopDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Address> Addresses => Set<Address>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasIndex(c => c.Email).IsUnique();
            e.Property(c => c.Email).HasMaxLength(256);
            e.Property(c => c.Role).HasConversion<string>().HasMaxLength(32);
            e.HasMany(c => c.Addresses).WithOne(a => a.Customer).HasForeignKey(a => a.CustomerId);
            e.HasMany(c => c.PaymentMethods).WithOne(p => p.Customer).HasForeignKey(p => p.CustomerId);
            e.HasMany(c => c.Orders).WithOne(o => o.Customer).HasForeignKey(o => o.CustomerId);
        });

        modelBuilder.Entity<Address>(e => e.Property(a => a.Kind).HasConversion<string>().HasMaxLength(16));

        modelBuilder.Entity<PaymentMethod>(e => e.Property(p => p.Type).HasConversion<string>().HasMaxLength(16));

        modelBuilder.Entity<Category>(e =>
            e.HasOne(c => c.ParentCategory).WithMany().HasForeignKey(c => c.ParentCategoryId));

        modelBuilder.Entity<Product>(e =>
        {
            e.HasIndex(p => p.Sku).IsUnique();
            e.Property(p => p.UnitPrice).HasPrecision(18, 2);
            e.HasOne(p => p.Category).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasIndex(o => o.OrderNumber).IsUnique();
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(o => o.SubtotalAmount).HasPrecision(18, 2);
            e.Property(o => o.DiscountAmount).HasPrecision(18, 2);
            e.Property(o => o.TotalAmount).HasPrecision(18, 2);
            e.OwnsOne(o => o.ShippingAddress);
            e.OwnsOne(o => o.BillingAddress);
            e.HasMany(o => o.Lines).WithOne().HasForeignKey(l => l.OrderId);
            e.HasOne(o => o.Payment).WithOne().HasForeignKey<Payment>(p => p.OrderId);
            e.HasMany(o => o.StatusHistory).WithOne().HasForeignKey(h => h.OrderId);
        });

        modelBuilder.Entity<OrderLine>(e => e.Property(l => l.UnitPrice).HasPrecision(18, 2));

        modelBuilder.Entity<Payment>(e =>
        {
            e.Property(p => p.Amount).HasPrecision(18, 2);
            e.Property(p => p.Method).HasConversion<string>().HasMaxLength(16);
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<OrderStatusHistoryEntry>(e =>
            e.Property(h => h.Status).HasConversion<string>().HasMaxLength(16));
    }
}
