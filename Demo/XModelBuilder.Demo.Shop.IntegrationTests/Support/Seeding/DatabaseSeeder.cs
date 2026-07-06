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
    public static void Seed(ShopDbContext db, IModelBuilderProvider xprovider)
    {
        var electronics = xprovider
            .For<Category>()
            .With(c => c.Name, "Electronics")
            .Build();

        var phones = xprovider
            .For<Category>()
            .With(c => c.Name, "Phones")
            .With(c => c.ParentCategory, electronics)
            .Build();

        var books = xprovider
            .For<Category>()
            .With(c => c.Name, "Books")
            .Build();

        var phone = xprovider.For<Product>("product")
            .With(p => p.Sku, "SKU-PHONE-1").With(p => p.Name, "Demo Phone")
            .With(p => p.UnitPrice, 500m).With(p => p.StockQuantity, 10)
            .With(p => p.Category, phones).Build();

        var book = xprovider.For<Product>("product")
            .With(p => p.Sku, "SKU-BOOK-1").With(p => p.Name, "Demo Book")
            .With(p => p.UnitPrice, 20m).With(p => p.StockQuantity, 3)
            .With(p => p.Category, books).Build();

        var charger = xprovider.For<Product>("product")
            .With(p => p.Sku, "SKU-CHARGER-1").With(p => p.Name, "Demo Charger")
            .With(p => p.UnitPrice, 25m).With(p => p.StockQuantity, 50)
            .With(p => p.Category, electronics).Build();

        db.Products.AddRange(phone, book, charger);

        db.Customers.Add(xprovider.BuildCustomer("customer", "alice@shop.test", "Alice Klant"));
        db.Customers.Add(xprovider.BuildCustomer("customer", "bob@shop.test", "Bob Klant"));
        db.Customers.Add(xprovider.BuildCustomer("warehouse", "wendy@shop.test", "Wendy Magazijn"));
        db.Customers.Add(xprovider.BuildCustomer("admin", "admin@shop.test", "Admin Beheer"));

        db.SaveChanges();
    }

    private static Customer BuildCustomer(this IModelBuilderProvider xprovider, string builderName, string email, string fullName)
    {
        var adresses = xprovider
            .ForEmpty<Address>()
            .BuildMany(2, (b, i) => b
                .With(x => x.Kind, i == 0 ? AddressKind.Shipping : AddressKind.Billing)
                .With(x => x.Street, "Hoofdstraat")
                .With(x => x.HouseNumber, "1")
                .With(x => x.PostalCode, "1000 AA")
                .With(x => x.City, "Amsterdam")
            );

        return xprovider.For<Customer>(builderName)
            .With(c => c.Email, email)
            .With(c => c.FullName, fullName)
            .With(c => c.Addresses, adresses)
            .With(c => c.PaymentMethods, xm => 
            [
                xm
                    .For<PaymentMethod>()
                    .With(p => p.Type, PaymentMethodType.CreditCard)
                    .With(p => p.Display, "Credit Card - Visa")
                    .Build()
            ])
            .Build();
    }
}
