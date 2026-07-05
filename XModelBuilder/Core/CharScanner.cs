using System.Linq;

namespace XModelBuilder.Core
{
    /// <summary>
    /// A forward-only character scanner over a source string. Provides low-level
    /// primitives (peek, next, expect, skip) used by the mini-datalanguage parser
    /// to tokenize input, together with position tracking and contextual error reporting.
    /// </summary>
    /// <param name="value">
    /// The source string to scan. A <see langword="null"/> value is treated as an empty string.
    /// </param>
    internal class CharScanner(string value)
    {
        /// <summary>
        /// Represents end-of-file. Returned when no more characters are available.
        /// </summary>
        public const int EOF = -1;

        /// <summary>
        /// The underlying character source.
        /// </summary>
        private readonly string _source = value ?? string.Empty;

        /// <summary>
        /// Current index within the source string.
        /// </summary>
        private int _idx = 0;

        /// <summary>
        /// Gets the inner source
        /// </summary>
        public string Source => _source;

        /// <summary>
        /// Get the inner index
        /// </summary>
        public int Pos => _idx;

        /// <summary>
        /// Returns true if reading at the given offset would go past the end of the source.
        /// </summary>
        /// <param name="offset">The number of characters ahead of the current position to test.</param>
        /// <returns><see langword="true"/> when the position plus offset lies beyond the source; otherwise <see langword="false"/>.</returns>
        public bool Eof(int offset = 0)
        {
            return _idx + offset >= _source.Length;
        }

        /// <summary>
        /// Peeks at the character at the current position + offset without advancing.
        /// Returns EOF if out of range.
        /// </summary>
        /// <param name="offset">The number of characters ahead of the current position to inspect.</param>
        /// <returns>The character code at that position, or <see cref="EOF"/> when it lies beyond the source.</returns>
        public int Peek(int offset = 0)
        {
            return !Eof(offset) ? _source[_idx + offset] : EOF;
        }

        /// <summary>
        /// Reads the next character and ensures it matches the expected value.
        /// </summary>
        /// <param name="expected">The character code that the next character must equal (may be <see cref="EOF"/>).</param>
        /// <returns>This scanner, to allow call chaining.</returns>
        /// <exception cref="FormatException">Thrown when the next character does not match <paramref name="expected"/>.</exception>
        public CharScanner Expect(int expected)
        {
            var next = Next();
            if (next != expected)
            {
                throw ParseError($"invalid char {ToCharString(next)}, expected '{ToCharString(expected)}'.");
            }
            return this;
        }

        /// <summary>
        /// Reads the next character and ensures the end of the source has been reached.
        /// </summary>
        /// <returns>This scanner, to allow call chaining.</returns>
        /// <exception cref="FormatException">Thrown when any character remains in the source.</exception>
        public CharScanner ExpectEof() => Expect(EOF);

        /// <summary>
        /// Reads the next character and ensures it matches one of the expected values.
        /// When no expected values are supplied nothing is consumed and the scanner is
        /// returned unchanged.
        /// </summary>
        /// <param name="expected">The set of acceptable character codes for the next character.</param>
        /// <returns>This scanner, to allow call chaining.</returns>
        /// <exception cref="FormatException">Thrown when the next character matches none of <paramref name="expected"/>.</exception>
        public CharScanner ExpectAnyOf(params int[] expected)
        {
            if (expected.Length == 0)
            {
                return this;
            }

            var chr = Next();
            foreach (var c in expected)
            {
                if (chr == c)
                {
                    return this;
                }
            }
            throw ParseError($"invalid char {ToCharString(chr)}, expected any of: [{string.Join(",", expected.Select(c => $"'{ToCharString(c)}'"))}]");
        }

        /// <summary>
        /// Returns true if the character at the current position matches any of the
        /// expected values, without advancing.
        /// </summary>
        /// <param name="expected">The set of character codes to test against.</param>
        /// <returns><see langword="true"/> when the current character matches one of <paramref name="expected"/>; <see langword="false"/> when it matches none or none are supplied.</returns>
        public bool PeekIsAnyOf(params int[] expected) => PeekIsAnyOfWithOffset(0, expected);

        /// <summary>
        /// Returns true if the character at the current position plus the given offset
        /// matches any of the expected values, without advancing.
        /// </summary>
        /// <param name="offset">The number of characters ahead of the current position to inspect.</param>
        /// <param name="expected">The set of character codes to test against.</param>
        /// <returns><see langword="true"/> when the character at that offset matches one of <paramref name="expected"/>; <see langword="false"/> when it matches none or none are supplied.</returns>
        public bool PeekIsAnyOfWithOffset(int offset, params int[] expected)
        {
            if (expected.Length == 0)
            {
                return false;
            }

            var chr = Peek(offset);
            foreach (var c in expected)
            {
                if (chr == c)
                {
                    return true;
                }
            }
            return false;
        }


        /// <summary>
        /// Reads the next character and advances the index.
        /// </summary>
        /// <returns>The consumed character code, or <see cref="EOF"/> when the end of the source has been reached.</returns>
        public int Next()
        {
            return Eof() ? EOF : _source[_idx++];
        }

        /// <summary>
        /// Reads the next character, ensures it matches the expected value or one of
        /// the alternatives, and returns it.
        /// </summary>
        /// <param name="expected">The primary acceptable character code.</param>
        /// <param name="others">Additional acceptable character codes.</param>
        /// <returns>The consumed character code.</returns>
        /// <exception cref="FormatException">Thrown when the next character matches neither <paramref name="expected"/> nor any of <paramref name="others"/>.</exception>
        public int NextAnyOf(int expected, params int[] others)
        {

            var chr = Next();

            if (chr == expected)
            {
                return chr;
            }

            foreach (var c in others)
            {
                if (chr == c)
                {
                    return chr;
                }
            }
            throw ParseError($"invalid char {ToCharString(chr)}, expected any of: [{string.Join(",", new[] { expected }.Concat(others).Select(c => $"'{ToCharString(c)}'"))}]");
        }


        /// <summary>
        /// Advances the index past any insignificant whitespace characters
        /// (space, tab, newline, carriage return).
        /// </summary>
        /// <returns>This scanner, positioned on the first non-whitespace character or at end of source, to allow call chaining.</returns>
        public CharScanner SkipInsignificants()
        {
            do
            {
                switch (Peek())
                {
                    case ' ':
                    case '\t':
                    case '\n':
                    case '\r':
                        _idx++;
                        continue;
                    default:
                        return this;
                }
            } while (true);
        }

        /// <summary>
        /// Creates a <see cref="FormatException"/> describing a parse error at the current
        /// position, including a short surrounding fragment of the source for context.
        /// </summary>
        /// <param name="msg">The message describing what went wrong.</param>
        /// <returns>A <see cref="FormatException"/> for the caller to throw; this method does not throw it itself.</returns>
        public FormatException ParseError(string msg)
        {
            var fragment = "";
            var fragmentStartsAt = Math.Max(_idx - 3, 0);
            var fragmentEndsAt = Math.Min(_idx + 3, _source.Length - 1);
            if (fragmentEndsAt > fragmentStartsAt)
            {
                fragment = _source[fragmentStartsAt..fragmentEndsAt];
            }
            return new FormatException($"\'...{fragment}...\': error at pos {_idx}: {msg}");
        }

        /// <summary>
        /// Renders a character code as a display string: the literal "EOF" for the
        /// end-of-file marker, otherwise the single character itself.
        /// </summary>
        /// <param name="chr">The character code to render (may be <see cref="EOF"/>).</param>
        /// <returns>The string "EOF" when <paramref name="chr"/> is <see cref="EOF"/>; otherwise a one-character string containing the character.</returns>
        public static string ToCharString(int chr)
        {
            return chr == EOF ? nameof(EOF) : new string((char)chr, 1);
        }
    }
}
