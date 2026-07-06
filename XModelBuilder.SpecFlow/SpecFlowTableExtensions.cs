using System.Linq.Expressions;
using TechTalk.SpecFlow;

namespace XModelBuilder.SpecFlow;

/// <summary>
/// Builds XModelBuilder models from a Gherkin <see cref="Table"/>, intelligently supporting
/// both common table shapes:
/// <list type="bullet">
/// <item>
/// <description>
/// Vertical "Field/Value" tables (exactly two columns, header matching a well-known
/// field/value naming convention): every row describes one member of a SINGLE instance, e.g.
/// <code>
/// | Field | Value     |
/// | Name  | John      |
/// | City  | Amsterdam |
/// </code>
/// </description>
/// </item>
/// <item>
/// <description>
/// Horizontal tables (any other shape): the header row holds the member names and every
/// data row describes one instance, e.g.
/// <code>
/// | Name | City      |
/// | John | Amsterdam |
/// | Jane | Utrecht   |
/// </code>
/// </description>
/// </item>
/// </list>
/// Each row is applied via <see cref="IModelBuilder{TModel}.WithValues"/>, so the same
/// conversion rules (deep paths, arrays, object literals, null()/new()/default()/named
/// builder references) apply as everywhere else in XModelBuilder.
/// </summary>
public static class SpecFlowTableExtensions
{
    private static IReadOnlyList<VerticalTableHeader> _verticalTableHeaders =
    [
        new("field", "value"),
        new("key", "value"),
        new("name", "value"),
        new("property", "value"),
        // Dutch
        new("veld", "waarde"),
        new("eigenschap", "waarde"),
        new("sleutel", "waarde"),
    ];

    /// <summary>
    /// The accepted column-name pairs that mark a two-column table as a VERTICAL "field/value" table
    /// (one member per row) instead of a horizontal one. Language-dependent (English + Dutch by default).
    /// Read-only here; change it via <see cref="Configure"/>. Comparison is case-insensitive.
    /// </summary>
    public static IReadOnlyList<VerticalTableHeader> VerticalTableHeaders => _verticalTableHeaders;

    /// <summary>
    /// Configures the SpecFlow table integration - currently the language-dependent vertical
    /// <see cref="VerticalTableHeaders"/>. This is a PROCESS-WIDE setting (the integration registers no
    /// services), so call it once, typically at test-run start:
    /// <code>
    /// SpecFlowTableExtensions.Configure(o => o.VerticalTableHeaders.Add(new("champ", "valeur")));
    /// </code>
    /// </summary>
    public static void Configure(Action<SpecFlowTableOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var options = new SpecFlowTableOptions
        {
            VerticalTableHeaders = [.. _verticalTableHeaders],
        };
        configure(options);
        _verticalTableHeaders = [.. options.VerticalTableHeaders];
    }

    /// <summary>
    /// Builds a single <typeparamref name="TModel"/> instance from <paramref name="table"/> by
    /// applying its row(s) to <paramref name="builder"/> via <see cref="IModelBuilder{TModel}.WithValues"/>.
    /// Any configuration already applied to <paramref name="builder"/> (e.g. via With(...)) is
    /// preserved; the table's values are applied on top of it. Throws
    /// <see cref="InvalidOperationException"/> if the table is horizontal and does not contain
    /// exactly one data row (use <see cref="CreateModels{TModel}"/> on the provider for that case).
    /// </summary>
    public static TModel CreateModel<TModel>(this IModelBuilder<TModel> builder, Table table)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var rows = ToFieldValueRows(table);

        if (rows.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one row to build a single {typeof(TModel).Name}, but the table describes {rows.Count}. " +
                $"Use {nameof(CreateModels)}<{typeof(TModel).Name}>() on the provider to build a list instead.");
        }

        return builder.WithValues(rows[0]).Build();
    }

    /// <summary>
    /// Sets a single <paramref name="member"/> to a <typeparamref name="TValue"/> built from
    /// <paramref name="table"/> (via <typeparamref name="TValue"/>'s own builder), then continues the
    /// fluent chain. Lets you fill a nested member from its OWN table instead of cramming everything into
    /// one table, e.g. <c>builder.With(k =&gt; k.Naam, "Alice").WithValue(k =&gt; k.Adres, adresTabel)</c>.
    /// </summary>
    public static IModelBuilder<TModel> WithValue<TModel, TValue>(
        this IModelBuilder<TModel> builder,
        Expression<Func<TModel, TValue>> member,
        Table table)
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(table);

        return builder.With(member, xprovider => xprovider.For<TValue>().CreateModel(table));
    }

    /// <summary>
    /// Extends an existing <paramref name="instance"/> by setting a single nested <paramref name="member"/>
    /// to a <typeparamref name="TValue"/> built from <paramref name="table"/>, and returns the instance.
    /// The set is applied through a fresh built-in <c>DefaultModelBuilder&lt;TModel&gt;</c>
    /// (see <see cref="IModelBuilderProvider.ForEmpty{TModel}"/>), so NONE of
    /// <typeparamref name="TModel"/>'s own builder defaults or computed logic run - only this one member is
    /// touched. Handy to compose a model over multiple Gherkin tables across steps.
    /// </summary>
    public static TModel Extend<TModel, TValue>(
        this IModelBuilderProvider provider,
        TModel instance,
        Expression<Func<TModel, TValue>> member,
        Table table)
        where TModel : class
        where TValue : class
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(instance);
        ArgumentNullException.ThrowIfNull(table);

        return provider.ForEmpty<TModel>()
            .WithValue(member, table)
            .Extend(instance);
    }

    /// <summary>
    /// Builds one <typeparamref name="TModel"/> instance per row described by
    /// <paramref name="table"/>, each via its own fresh builder (from
    /// <see cref="IModelBuilderProvider.For{TModel}()"/>). A vertical "Field/Value" table
    /// always describes exactly one instance, so this returns a single-element list in that case.
    /// </summary>
    public static IReadOnlyList<TModel> CreateModels<TModel>(this IModelBuilderProvider provider, Table table)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(provider);

        return CreateModels(table, () => provider.For<TModel>());
    }

    /// <summary>
    /// Same as <see cref="CreateModels{TModel}(IModelBuilderProvider, Table)"/>, but resolves the
    /// builder for each row through the model builder explicitly registered under
    /// <see cref="ModelBuilderAttribute"/> name <paramref name="modelBuilderName"/>, instead of
    /// whichever builder currently counts as "default" for <typeparamref name="TModel"/>.
    /// </summary>
    public static IReadOnlyList<TModel> CreateModels<TModel>(this IModelBuilderProvider provider, Table table, string modelBuilderName)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(provider);

        return CreateModels(table, () => provider.For<TModel>(modelBuilderName));
    }

    private static IReadOnlyList<TModel> CreateModels<TModel>(Table table, Func<IModelBuilder<TModel>> resolveBuilder)
        where TModel : class
    {
        return ToFieldValueRows(table)
            .Select(row => resolveBuilder().WithValues(row).Build())
            .ToList();
    }

    /// <summary>
    /// Normalizes either table shape into a list of field/value rows: a vertical table
    /// always yields exactly one row (all of its lines combined); a horizontal table yields
    /// one row per data line, using the header as field names. Whether a two-column table counts as
    /// vertical is decided by the configurable <see cref="VerticalTableHeaders"/>.
    /// </summary>
    private static List<List<KeyValuePair<string, string?>>> ToFieldValueRows(Table table)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (IsVerticalFieldValueTable(table))
        {
            var fields = table.Rows
                .Select(row => new KeyValuePair<string, string?>(row[0], row[1]))
                .ToList();

            return [fields];
        }

        var headers = table.Header.ToList();

        return table.Rows
            .Select(row => headers
                .Select(header => new KeyValuePair<string, string?>(header, row[header]))
                .ToList())
            .ToList();
    }

    private static bool IsVerticalFieldValueTable(Table table)
    {
        if (table.Header.Count != 2)
        {
            return false;
        }

        var headers = table.Header.Select(h => h.Trim().ToLowerInvariant()).ToArray();
        return VerticalTableHeaders.Any(c =>
            headers[0] == c.FieldColumn.Trim().ToLowerInvariant() &&
            headers[1] == c.ValueColumn.Trim().ToLowerInvariant());
    }
}
