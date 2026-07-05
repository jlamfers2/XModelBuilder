using Microsoft.Extensions.DependencyInjection;
using TechTalk.SpecFlow;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.SpecFlow.UnitTests;

// De verticale-tabel-kolomnamen zijn taalafhankelijk en dus CONFIGUREERBAAR (niet hardcoded):
// SpecFlowTableExtensions.VerticalTableHeaders.
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
        var original = SpecFlowTableExtensions.VerticalTableHeaders;
        try
        {
            // Voeg een Franse conventie toe (defaults blijven behouden).
            SpecFlowTableExtensions.Configure(o => o.VerticalTableHeaders.Add(new("champ", "valeur")));

            var table = new Table("Champ", "Valeur");
            table.AddRow("Name", "John");
            table.AddRow("City", "Amsterdam");

            // Zonder de conventie zou dit als een horizontale tabel (2 rijen) gelezen worden en falen.
            var person = CreateProvider().For<Person>().CreateModel(table);

            Assert.Equal("John", person.Name);
            Assert.Equal("Amsterdam", person.City);
        }
        finally
        {
            SpecFlowTableExtensions.Configure(o => o.VerticalTableHeaders = [.. original]);
        }
    }

    [Fact]
    public void UnconfiguredHeader_IsTreatedAsHorizontal()
    {
        // "Champ/Valeur" staat NIET in de default-conventies -> horizontaal -> 2 datarijen -> CreateModel faalt.
        var table = new Table("Champ", "Valeur");
        table.AddRow("Name", "John");
        table.AddRow("City", "Amsterdam");

        Assert.Throws<InvalidOperationException>(() => CreateProvider().For<Person>().CreateModel(table));
    }
}
