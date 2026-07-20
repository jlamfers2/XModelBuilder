using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reqnroll;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.Reqnroll.UnitTests;

public class ReqnrollTableExtensionsTests
{
    public class Person
    {
        public string Name { get; set; } = null!;
        public string City { get; set; } = null!;
        public string Country { get; set; } = null!;
    }

    [ModelBuilder("person")]
    public sealed class PersonBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<PersonBuilder, Person>(options, xprovider)
    {
        protected override void SetDefaults()
        {
        }
    }

    [ModelBuilder("dutch-person")]
    public sealed class DutchPersonBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<DutchPersonBuilder, Person>(options, xprovider)
    {
        protected override void SetDefaults()
        {
            With(p => p.Country, "NL");
        }
    }

    private static IModelBuilderProvider CreateProvider() =>
        new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<PersonBuilder>()
            .AddModelBuilder<DutchPersonBuilder>()
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

    [Fact]
    public void CreateModel_OnProviderFor_FromVerticalFieldValueTable_BuildsSingleInstance()
    {
        // Arrange
        var table = new Table("Field", "Value");
        table.AddRow("Name", "John");
        table.AddRow("City", "Amsterdam");

        // Act
        var person = CreateProvider().For<Person>().CreateModel(table);

        // Assert
        Assert.Equal("John", person.Name);
        Assert.Equal("Amsterdam", person.City);
    }

    [Fact]
    public void CreateModel_FromDutchVerticalVeldWaardeTable_BuildsSingleInstance()
    {
        // Arrange
        var table = new Table("Veld", "Waarde");
        table.AddRow("Name", "John");
        table.AddRow("City", "Amsterdam");

        // Act
        var person = CreateProvider().For<Person>().CreateModel(table);

        // Assert
        Assert.Equal("John", person.Name);
        Assert.Equal("Amsterdam", person.City);
    }

    [Fact]
    public void CreateModel_OnProviderUse_FromHorizontalSingleRowTable_BuildsSingleInstance()
    {
        // Arrange
        var table = new Table("Name", "City");
        table.AddRow("John", "Amsterdam");

        // Act
        var person = CreateProvider().Use<PersonBuilder>().CreateModel(table);

        // Assert
        Assert.Equal("John", person.Name);
        Assert.Equal("Amsterdam", person.City);
    }

    [Fact]
    public void CreateModel_PreservesPriorManualConfiguration()
    {
        // Arrange
        var table = new Table("Name", "City");
        table.AddRow("John", "Amsterdam");

        // Act
        var person = CreateProvider().For<Person>()
            .With(p => p.Country, "NL")
            .CreateModel(table);

        // Assert
        Assert.Equal("John", person.Name);
        Assert.Equal("Amsterdam", person.City);
        Assert.Equal("NL", person.Country);
    }

    [Fact]
    public void CreateModel_FromHorizontalMultiRowTable_Throws()
    {
        // Arrange
        var table = new Table("Name", "City");
        table.AddRow("John", "Amsterdam");
        table.AddRow("Jane", "Utrecht");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => CreateProvider().For<Person>().CreateModel(table));
    }

    [Fact]
    public void CreateModels_OnProvider_FromHorizontalTable_BuildsOnePerRow()
    {
        // Arrange
        var table = new Table("Name", "City");
        table.AddRow("John", "Amsterdam");
        table.AddRow("Jane", "Utrecht");

        // Act
        var people = CreateProvider().CreateModels<Person>(table);

        // Assert
        Assert.Equal(2, people.Count);
        Assert.Equal("John", people[0].Name);
        Assert.Equal("Amsterdam", people[0].City);
        Assert.Equal("Jane", people[1].Name);
        Assert.Equal("Utrecht", people[1].City);
    }

    [Fact]
    public void CreateModels_OnProvider_FromVerticalFieldValueTable_ReturnsSingleElementList()
    {
        // Arrange
        var table = new Table("Field", "Value");
        table.AddRow("Name", "John");
        table.AddRow("City", "Amsterdam");

        // Act
        var people = CreateProvider().CreateModels<Person>(table);

        // Assert
        Assert.Single(people);
        Assert.Equal("John", people[0].Name);
        Assert.Equal("Amsterdam", people[0].City);
    }

    [Fact]
    public void CreateModels_WithNamedBuilder_UsesThatBuilderForEveryRow()
    {
        // Arrange
        var table = new Table("Name", "City");
        table.AddRow("John", "Amsterdam");
        table.AddRow("Jane", "Utrecht");

        // Act
        var people = CreateProvider().CreateModels<Person>(table, "dutch-person");

        // Assert
        Assert.Equal(2, people.Count);
        Assert.Equal("NL", people[0].Country);
        Assert.Equal("NL", people[1].Country);
    }

    [Fact]
    public void CreateModels_WithUnknownBuilderName_Throws()
    {
        // Arrange
        var table = new Table("Name", "City");
        table.AddRow("John", "Amsterdam");

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() =>
            CreateProvider().CreateModels<Person>(table, "does-not-exist"));
    }
}
