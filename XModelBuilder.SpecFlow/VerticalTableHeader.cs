namespace XModelBuilder.SpecFlow;

/// <summary>
/// One accepted header for a vertical "field/value" table: the name of the column holding member
/// names (<see cref="FieldColumn"/>) and the one holding their values (<see cref="ValueColumn"/>).
/// Configure the accepted set via <see cref="SpecFlowTableExtensions.VerticalTableHeaders"/>.
/// </summary>
public readonly record struct VerticalTableHeader(string FieldColumn, string ValueColumn);
