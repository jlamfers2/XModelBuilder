namespace XModelBuilder.Core
{
    /// <summary>
    /// Stateless facade over <see cref="DataParser"/> for parsing the mini-datalanguage.
    /// Each call uses a fresh parser instance, so the methods are safe to call concurrently.
    /// </summary>
    internal static class Parser
    {
        /// <summary>
        /// Parses an array from the given text, accepting both the delimited form (<c>[a, b, c]</c>)
        /// and the top-level "bare" form without surrounding brackets (<c>a, b, c</c>).
        /// </summary>
        /// <param name="text">The text fragment to parse.</param>
        /// <returns>The parsed elements as an <see cref="object"/> array.</returns>
        /// <exception cref="FormatException">Thrown when the text is malformed or contains trailing content after the array.</exception>
        public static object[] ParseArray(string text) => new DataParser().ParseArray(text);

        /// <summary>
        /// Parses an object from the given text, accepting both the delimited form
        /// (<c>{ key: value, ... }</c>) and the top-level "bare" form without surrounding braces
        /// (<c>key: value, ...</c>). Keys are compared case-insensitively.
        /// </summary>
        /// <param name="text">The text fragment to parse.</param>
        /// <returns>The parsed key/value pairs.</returns>
        /// <exception cref="FormatException">Thrown when the text is malformed, a key is empty, or content trails after the object.</exception>
        public static IEnumerable<KeyValuePair<string,object>> ParseObject(string text) => new DataParser().ParseObject(text);

        /// <summary>
        /// Returns true when the text looks like a "bare" object (<c>key:value,...</c>) without
        /// surrounding <c>{ }</c>: there is a ':' at the top level, outside strings and outside
        /// <c>[ ]</c> / <c>{ }</c>.
        /// </summary>
        /// <param name="text">The text fragment to inspect.</param>
        /// <returns><see langword="true"/> when a top-level assignment ':' is found; otherwise <see langword="false"/>.</returns>
        public static bool LooksLikeBareObject(string text) => DataParser.LooksLikeBareObject(text);
    }
}
