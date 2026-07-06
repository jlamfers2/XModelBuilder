using Microsoft.Extensions.DependencyInjection;
using Reqnroll;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.Reqnroll.UnitTests;

// De verticale-tabel-kolomnamen zijn taalafhankelijk en dus CONFIGUREERBAAR (niet hardcoded):
// ReqnrollTableExtensions.VerticalTableHeaders.
public class ConfigurableVerticalHeaderTests
{
    public class Person
    {
        public string Name { get; set; } = "";
        public string City { get; set; } = "";
    }

    private static IModelBuilderProvider CreateProvider() =>
        new ServiceCollection()
            .AddXModelBuilder()
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

    [Fact]
    public void VerticalTableHeaders_AreConfigurable_ForOtherLanguages()
    {
        // Arrange
        var original = ReqnrollTableExtensions.VerticalTableHeaders;
        try
        {
            // Voeg een Franse conventie toe (defaults blijven behouden).
            ReqnrollTableExtensions.Configure(o => o.VerticalTableHeaders.Add(new("champ", "valeur")));

            var table = new Table("Champ", "Valeur");
            table.AddRow("Name", "John");
            table.AddRow("City", "Amsterdam");

            // Act
            // Zonder de conventie zou dit als een horizontale tabel (2 rijen) gelezen worden en falen.
            var person = CreateProvider().For<Person>().CreateModel(table);

            // Assert
            Assert.Equal("John", person.Name);
            Assert.Equal("Amsterdam", person.City);
        }
        finally
        {
            ReqnrollTableExtensions.Configure(o => o.VerticalTableHeaders = [.. original]);
        }
    }

    [Fact]
    public void UnconfiguredHeader_IsTreatedAsHorizontal()
    {
        // Arrange
        // "Champ/Valeur" staat NIET in de default-conventies -> horizontaal -> 2 datarijen -> CreateModel faalt.
        var table = new Table("Champ", "Valeur");
        table.AddRow("Name", "John");
        table.AddRow("City", "Amsterdam");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => CreateProvider().For<Person>().CreateModel(table));
    }
}
