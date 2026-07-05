namespace XModelBuilder.SpecFlow;

/// <summary>
/// Configuration for the SpecFlow table integration, passed to
/// <see cref="SpecFlowTableExtensions.Configure"/>.
/// </summary>
public sealed class SpecFlowTableOptions
{
    /// <summary>
    /// The accepted column-name pairs that mark a two-column table as a VERTICAL "field/value" table.
    /// Seeded with the current conventions; add your language's pair, or replace the list wholesale.
    /// Comparison is case-insensitive.
    /// </summary>
    public IList<VerticalTableHeader> VerticalTableHeaders { get; set; } = new List<VerticalTableHeader>();
}
