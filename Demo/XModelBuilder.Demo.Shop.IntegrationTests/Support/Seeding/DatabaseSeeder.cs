using XModelBuilder.Demo.Shop.Data;
using XModelBuilder.Demo.Shop.Domain;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Support.Seeding;

/// <summary>
/// Builds and commits the initial dataset ONCE. Entities are built with XModelBuilder (the customers
/// via the role-specific named builders), demonstrating the library on the seed itself. Every
/// scenario starts from exactly this state and rolls back to it afterwards.
/// </summary>
public static class DatabaseSeeder
{
    public static void Seed(ShopDbContext db, IModelBuilderProvider xmodels)
    {
        var electronics = new Category { Name = "Electronics" };
        var phones = new Category { Name = "Phones", ParentCategory = electronics };
        var books = new Category { Name = "Books" };

        var phone = xmodels.For<Product>("product")
            .With(p => p.Sku, "SKU-PHONE-1").With(p => p.Name, "Demo Phone")
            .With(p => p.UnitPrice, 500m).With(p => p.StockQuantity, 10)
            .With(p => p.Category, phones).Build();

        var book = xmodels.For<Product>("product")
            .With(p => p.Sku, "SKU-BOOK-1").With(p => p.Name, "Demo Book")
            .With(p => p.UnitPrice, 20m).With(p => p.StockQuantity, 3)
            .With(p => p.Category, books).Build();

        var charger = xmodels.For<Product>("product")
            .With(p => p.Sku, "SKU-CHARGER-1").With(p => p.Name, "Demo Charger")
            .With(p => p.UnitPrice, 25m).With(p => p.StockQuantity, 50)
            .With(p => p.Category, electronics).Build();

        db.Products.AddRange(phone, book, charger);

        db.Customers.Add(BuildCustomer(xmodels, "customer", "alice@shop.test", "Alice Klant"));
        db.Customers.Add(BuildCustomer(xmodels, "customer", "bob@shop.test", "Bob Klant"));
        db.Customers.Add(BuildCustomer(xmodels, "warehouse", "wendy@shop.test", "Wendy Magazijn"));
        db.Customers.Add(BuildCustomer(xmodels, "admin", "admin@shop.test", "Admin Beheer"));

        db.SaveChanges();
    }

    private static Customer BuildCustomer(IModelBuilderProvider xmodels, string builderName, string email, string fullName) =>
        xmodels.For<Customer>(builderName)
            .With(c => c.Email, email)
            .With(c => c.FullName, fullName)
            .With(c => c.Addresses,
            [
                new Address { Kind = AddressKind.Shipping, Street = "Hoofdstraat", HouseNumber = "1", PostalCode = "1000 AA", City = "Amsterdam" },
                new Address { Kind = AddressKind.Billing, Street = "Hoofdstraat", HouseNumber = "1", PostalCode = "1000 AA", City = "Amsterdam" },
            ])
            .With(c => c.PaymentMethods,
            [
                new PaymentMethod { Type = PaymentMethodType.Ideal, Display = "iDEAL - ING" },
            ])
            .Build();
}
