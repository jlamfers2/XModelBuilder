using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests
{
    public class ModelBuilder_old_Tests
    {

        public class Widget
        {
            public Guid Id { get; }
            public Guid Id2 { get; }

            public Widget(Guid id2)
            {
                Id2 = id2;
            }
        }

        public class WidgetModelBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels) : ModelBuilder<WidgetModelBuilder, Widget>(options, xmodels)
        {
            protected override void SetDefaults()
            {
                With(x => x.Id, Guid.NewGuid);
                With(x => x.Id2, Guid.NewGuid);
            }
        }


        public class Address
        {
            public string Street { get; set; } = null!;
            public string StreetNumber { get; set; } = null!;
            public string PostalCode { get; set; } = null!;
            public string City { get; set; } = null!;
        }

        public class Vehicle
        {
            public Address? GarageAddress { get; set; }
        }

        [ModelBuilder("complex-adres")]
        public sealed class ComplexAddressBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
            : ModelBuilder<ComplexAddressBuilder, Address>(options, xmodels)
        {
            protected override void SetDefaults()
            {
                With(x => x.Street, "ComplexStreet");
                With(x => x.City, "ComplexCity");
            }
        }

        public class Person
        {
            public Person(Address address)
            {
                ArgumentNullException.ThrowIfNull(address);
                Address = address;
            }

            private readonly string _name = null!;
            public string Name { get => _name; }
            public string City { get; init; } = null!;
            public string[] Options { get; } = [];
            public Address Address { get; }
        }

#pragma warning disable S3453
        public class NoPublicCtorModel
#pragma warning restore S3453
        {
            public string Name { get; set; } = null!;

            private NoPublicCtorModel()
            {
            }
        }
        [Fact]
        public void Defaults_Are_ReInvoked_On_Each_Build()
        {
            // Arrange
            var sc = new ServiceCollection().AddXModelBuilder();

            sc.AddModelBuilder<WidgetModelBuilder>();

            var sp = sc.BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act
            var models = xmodels.For<Widget>().BuildMany(2);

            // Assert
            Assert.NotEqual(models[0].Id, models[1].Id);
            Assert.NotEqual(models[0].Id2, models[1].Id2);
        }

        [Fact]
        public void Init_Properties_Can_Be_Set()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act
            var model = xmodels.For<Person>()
                .With(x => x.Name, "John")
                .With(x => x.City, "Londen")
                .With(x => x.Options, ["gold-member","no-smoking"])
                .With(x => x.Address, b => b
                    .With(a => a.Street, "MainStreet")
                    .With(a => a.City, "City")
                )
                .Build();

            // Assert
            Assert.Equal("John", model.Name);
            Assert.Equal("MainStreet", model.Address.Street);
            Assert.Equal("City", model.Address.City);

            // Act (again, via string-path tokens)
            model = xmodels.For<Person>()
                .With("Name", "John")
                .With("City", "Londen")
                .With("Options", "[gold-member,no-smoking]")
                .With("Address", "{Street:\"MainStreet\",City:\"City\"}")
                .Build();

            // Assert
            Assert.Equal("John", model.Name);
            Assert.Equal("MainStreet", model.Address.Street);
            Assert.Equal("City", model.Address.City);
        }

        [Fact]
        public void Model_With_No_Public_Ctor_Falls_Back_To_Instantiator()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act
            var model = xmodels.For<NoPublicCtorModel>()
                .With(x => x.Name, "John")
                .Build();

            // Assert
            Assert.Equal("John", model.Name);
        }

        [Fact]
        public void With_Lambda_Value_From_Use_Builder()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .AddModelBuilder<ComplexAddressBuilder>()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act
            var model = xmodels.For<Person>()
                .With(x => x.Name, "John")
                .With(x => x.Address, xmodels.Use<ComplexAddressBuilder>().Build())
                .Build();

            // Assert
            Assert.Equal("ComplexStreet", model.Address.Street);
            Assert.Equal("ComplexCity", model.Address.City);
        }

        [Fact]
        public void With_StringPath_NamedBuilderReference_ResolvesExplicitBuilder()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .AddModelBuilder<ComplexAddressBuilder>()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act
            var model = xmodels.For<Person>()
                .With("Name", "John")
                .With("Address", "complex-adres")
                .Build();

            // Assert
            Assert.Equal("ComplexStreet", model.Address.Street);
            Assert.Equal("ComplexCity", model.Address.City);
        }

        [Fact]
        public void With_StringPath_NewToken_CreatesBlankInstance()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .AddModelBuilder<ComplexAddressBuilder>()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act
            var model = xmodels.For<Person>()
                .With("Name", "John")
                .With("Address", "new()")
                .Build();

            // Assert
            Assert.Null(model.Address.Street);
        }

        [Fact]
        public void With_StringPath_DefaultToken_EqualsForBuild()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act
            var model = xmodels.For<Person>()
                .With("Name", "John")
                .With("Address", "default()")
                .Build();

            var expectedAddress = xmodels.For<Address>().Build();

            // Assert
            Assert.Equal(expectedAddress.Street, model.Address.Street);
            Assert.Equal(expectedAddress.City, model.Address.City);
        }

        [Fact]
        public void With_StringPath_NullToken_SetsPropertyToNullExplicitly()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act
            var vehicle = xmodels.For<Vehicle>()
                .With(x => x.GarageAddress, new Address())
                .With("GarageAddress", "null()")
                .Build();

            // Assert
            Assert.Null(vehicle.GarageAddress);
        }

        [Fact]
        public void With_StringPath_UnknownNamedBuilderReference_Throws()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() =>
                xmodels.For<Person>()
                    .With("Name", "John")
                    .With("Address", "does-not-exist")
                    .Build());
        }

        [Fact]
        public void WithBuilder_ResolvesNamedBuilder_ViaLambda()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .AddModelBuilder<ComplexAddressBuilder>()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act
            var model = xmodels.For<Person>()
                .With(x => x.Name, "John")
                .WithBuilder(x => x.Address, "complex-adres")
                .Build();

            // Assert
            Assert.Equal("ComplexStreet", model.Address.Street);
            Assert.Equal("ComplexCity", model.Address.City);
        }

        [Fact]
        public void BuildMany_OnProvider_BuildsIndependentInstances_ConfiguredPerIndex()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act
            var people = xmodels.BuildMany<Person>(3, (b, i) => b
                .With(p => p.Name, $"Person{i}")
                .With(p => p.Address, new Address()));

            // Assert
            Assert.Equal(3, people.Count);
            Assert.Equal("Person0", people[0].Name);
            Assert.Equal("Person1", people[1].Name);
            Assert.Equal("Person2", people[2].Name);
        }

        [Fact]
        public void BuildMany_OnProvider_WithoutConfigure_BuildsCountIndependentDefaultInstances()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act
            var widgets = xmodels.BuildMany<NoPublicCtorModel>(4);

            // Assert
            Assert.Equal(4, widgets.Count);
        }

        [Fact]
        public void BuildMany_OnProvider_WithModelBuilderName_UsesThatBuilderForEveryInstance()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .AddModelBuilder<ComplexAddressBuilder>()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act
            var addresses = xmodels.BuildMany<Address>(3, "complex-adres");

            // Assert
            Assert.Equal(3, addresses.Count);
            Assert.All(addresses, a => Assert.Equal("ComplexStreet", a.Street));
        }

        [Fact]
        public void BuildMany_OnProvider_WithModelBuilderNameAndConfigure_CombinesBoth()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .AddModelBuilder<ComplexAddressBuilder>()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act
            var addresses = xmodels.BuildMany<Address>(3, "complex-adres", (b, i) => b.With(a => a.StreetNumber, i.ToString()));

            // Assert
            Assert.Equal(3, addresses.Count);
            Assert.Equal("ComplexStreet", addresses[0].Street);
            Assert.Equal("0", addresses[0].StreetNumber);
            Assert.Equal("1", addresses[1].StreetNumber);
            Assert.Equal("2", addresses[2].StreetNumber);
        }

        [Fact]
        public void BuildMany_OnBuilder_ReusesSameBuilder_VaryingStringPathValues()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            var builder = xmodels.For<Person>()
                .With(p => p.Address, new Address())
                .With(p => p.City, "Amsterdam");

            // Act
            var counter = 0;
            var people = builder
                .With(p => p.Name, () => $"Person{counter++}")
                .BuildMany(3);

            // Assert
            Assert.Equal(3, people.Count);
            Assert.Equal("Person0", people[0].Name);
            Assert.Equal("Person1", people[1].Name);
            Assert.Equal("Person2", people[2].Name);
            Assert.All(people, p => Assert.Equal("Amsterdam", p.City));
        }

        [Fact]
        public void With_ProviderValueFactory_ReceivesBuildersOwnProvider()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .AddModelBuilder<ComplexAddressBuilder>()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act
            var vehicle = xmodels.For<Vehicle>()
                .With(v => v.GarageAddress, provider => provider.For<Address>("complex-adres").Build())
                .Build();

            // Assert
            Assert.Equal("ComplexStreet", vehicle.GarageAddress!.Street);
        }

        [Fact]
        public void With_ProviderValueFactory_OnCtorBoundProperty_RoutesThroughHandleCtorArgument()
        {
            // Arrange
            var sp = new ServiceCollection()
                .AddXModelBuilder()
                .AddModelBuilder<ComplexAddressBuilder>()
                .BuildServiceProvider();

            var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

            // Act
            var person = xmodels.For<Person>()
                .With(p => p.Name, "John")
                .With(p => p.Address, provider => provider.For<Address>("complex-adres").Build())
                .Build();

            // Assert
            Assert.Equal("ComplexStreet", person.Address.Street);
        }
    }
}
