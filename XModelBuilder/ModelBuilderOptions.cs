using System.Globalization;

namespace XModelBuilder
{
    /// <summary>
    /// Configuration for how <see cref="ModelBuilder{TBuilder,TModel}"/> converts textual values:
    /// which cultures are used when parsing strings into typed members. Both default to
    /// <see cref="CultureInfo.InvariantCulture"/>.
    /// </summary>
    public class ModelBuilderOptions
    {
        /// <summary>
        /// The culture used when converting all non-date/time values (numbers, decimals, etc.).
        /// </summary>
        public CultureInfo DefaultCulture { get; set; } = CultureInfo.InvariantCulture;

        /// <summary>
        /// The culture used when converting <see cref="DateTime"/> and <see cref="DateTimeOffset"/> values.
        /// </summary>
        public CultureInfo DateTimeCulture { get; set; } = CultureInfo.InvariantCulture;
    }
}
