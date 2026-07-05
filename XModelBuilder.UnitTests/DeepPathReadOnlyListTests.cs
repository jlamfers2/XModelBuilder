using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

// Deep-path indexering in een collectie-member die default een VASTE-GROOTTE/read-only collectie is
// (bv. een init-only IReadOnlyList<> met default `[]`). De setter moet die materialiseren naar een
// groeibare List<> i.p.v. te falen met "Collection was of a fixed size".
public class DeepPathReadOnlyListTests
{
    public class Line
    {
        public string Sku { get; set; } = "";
        public int Aantal { get; set; }
    }

    public class Doc
    {
        public IReadOnlyList<Line> Lines { get; init; } = [];      // default = lege array (fixed size)
        public IReadOnlyList<string> Tags { get; init; } = [];
    }

    private static IModelBuilderProvider Provider() =>
        new ServiceCollection()
            .AddXModelBuilder()
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

    [Fact]
    public void IndexedDeepPath_IntoReadOnlyListOfObjects_GrowsAndSetsElements()
    {
        var doc = Provider().For<Doc>()
            .With("Lines[0].Sku", "BOEK1")
            .With("Lines[0].Aantal", "2")
            .With("Lines[1].Sku", "MOK1")
            .With("Lines[1].Aantal", "1")
            .Build();

        Assert.Equal(2, doc.Lines.Count);
        Assert.Equal("BOEK1", doc.Lines[0].Sku);
        Assert.Equal(2, doc.Lines[0].Aantal);
        Assert.Equal("MOK1", doc.Lines[1].Sku);
        Assert.Equal(1, doc.Lines[1].Aantal);
    }

    [Fact]
    public void IndexedDeepPath_IntoReadOnlyListOfScalars_GrowsAndSetsElements()
    {
        var doc = Provider().For<Doc>()
            .With("Tags[0]", "a")
            .With("Tags[2]", "c")     // gat op index 1 wordt opgevuld
            .Build();

        Assert.Equal(3, doc.Tags.Count);
        Assert.Equal("a", doc.Tags[0]);
        Assert.Equal("c", doc.Tags[2]);
    }
}
