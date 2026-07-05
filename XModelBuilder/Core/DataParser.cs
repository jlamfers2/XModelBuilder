using System.Text;

namespace XModelBuilder.Core
{
    /// <summary>
    /// Recursive-descent parser for the mini-datalanguage. Turns a text fragment into
    /// plain CLR objects: strings, <see cref="object"/> arrays and
    /// <see cref="Dictionary{TKey,TValue}"/> objects. Supports quoted and unquoted ("bare")
    /// values, and — at the top level — arrays and objects without their surrounding
    /// <c>[ ]</c> / <c>{ }</c> delimiters. Backed by a <see cref="CharScanner"/>.
    /// </summary>
    internal class DataParser
    {

        private static class Chars
        {
            public const int
                ArrayBegin = '[',
                ArrayEnd = ']',
                StringBegin = '"',
                StringEnd = '"',
                ObjectBegin = '{',
                ObjectEnd = '}',
                Separator = ',',
                Assignment = ':',
                Space = ' ',
                Cr = '\r',
                Nl = '\n',
                Tab = '\t',
                Escape = '\\';

            // Characters that terminate a "bare" (unquoted) value. Space and tab are DELIBERATELY NOT
            // included here: this lets an unquoted value contain internal spaces (e.g. "Clean Code",
            // "1234 AB"), which is what makes the "non-verbose" table form possible. The edge whitespace
            // is trimmed (see ReadBareValue). A value that itself contains the separator ',' (e.g. a
            // Dutch decimal "30,00") must still be quoted - the comma separates the fields/elements.
            public static bool IsReserved(int chr)
            {
                switch (chr)
                {
                    case ArrayBegin:
                    case ArrayEnd:
                    case StringBegin:
                    case ObjectBegin:
                    case ObjectEnd:
                    case Separator:
                    case Assignment:
                    case Escape:
                    case Cr:
                    case Nl:
                        return true;
                    default:
                        return false;
                }
            }
        }

        private CharScanner _scanner = null!;
        private readonly StringBuilder _sb = new();

        /// <summary>
        /// Parses a single value (string, array or object) from the given text and
        /// requires that the entire text is consumed.
        /// </summary>
        /// <param name="text">The text fragment to parse.</param>
        /// <returns>The parsed value as a plain CLR object: a <see cref="string"/>, an <see cref="object"/> array, or a <see cref="Dictionary{TKey,TValue}"/>.</returns>
        /// <exception cref="FormatException">Thrown when the text is malformed or contains trailing content after the value.</exception>
        public object Parse(string text)
        {
            _scanner = new CharScanner(text);
            var parseResult =  ReadNext();
            _scanner.SkipInsignificants().ExpectEof();
            return parseResult;
        }

        /// <summary>
        /// Parses an array from the given text, accepting both the delimited form
        /// (<c>[a, b, c]</c>) and the top-level "bare" form without surrounding brackets
        /// (<c>a, b, c</c>), and requires that the entire text is consumed.
        /// </summary>
        /// <param name="text">The text fragment to parse.</param>
        /// <returns>The parsed elements as an <see cref="object"/> array.</returns>
        /// <exception cref="FormatException">Thrown when the text is malformed or contains trailing content after the array.</exception>
        public object[] ParseArray(string text)
        {
            _scanner = new CharScanner(text).SkipInsignificants();
            var array = _scanner.Peek() == Chars.ArrayBegin ? ReadArray() : ReadBareArray();
            _scanner.SkipInsignificants().ExpectEof();
            return array;
        }

        /// <summary>
        /// Parses an object from the given text, accepting both the delimited form
        /// (<c>{ key: value, ... }</c>) and the top-level "bare" form without surrounding
        /// braces (<c>key: value, ...</c>), and requires that the entire text is consumed.
        /// Keys are compared case-insensitively.
        /// </summary>
        /// <param name="text">The text fragment to parse.</param>
        /// <returns>The parsed key/value pairs.</returns>
        /// <exception cref="FormatException">Thrown when the text is malformed, a key is empty, or content trails after the object.</exception>
        public IEnumerable<KeyValuePair<string, object>> ParseObject(string text)
        {
            _scanner = new CharScanner(text).SkipInsignificants();
            // Just like arrays, the delimiters may be omitted at the TOP level: a "bare" object without { }.
            // (Nested objects inside an array do keep their { } - that is the element boundary.)
            var obj = _scanner.Peek() == Chars.ObjectBegin ? ReadObject() : ReadBareObject();
            _scanner.SkipInsignificants().ExpectEof();
            return obj;
        }

        /// <summary>
        /// Returns true when the text looks like a "bare" object (<c>key:value,...</c>) without
        /// surrounding <c>{ }</c>: there is a ':' at the top level, outside strings and outside
        /// <c>[ ]</c> / <c>{ }</c>. A builder name (the other meaning of a bare string for a complex
        /// target type) never contains such a ':'.
        /// </summary>
        /// <param name="text">The text fragment to inspect.</param>
        /// <returns><see langword="true"/> when a top-level assignment ':' is found; otherwise <see langword="false"/>.</returns>
        public static bool LooksLikeBareObject(string text)
        {
            var depth = 0;
            var inString = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];

                if (inString)
                {
                    if (c == (char)Chars.Escape)
                    {
                        i++; // skip the escaped character
                    }
                    else if (c == (char)Chars.StringEnd)
                    {
                        inString = false;
                    }
                    continue;
                }

                switch (c)
                {
                    case (char)Chars.StringBegin:
                        inString = true;
                        break;
                    case (char)Chars.ArrayBegin:
                    case (char)Chars.ObjectBegin:
                        depth++;
                        break;
                    case (char)Chars.ArrayEnd:
                    case (char)Chars.ObjectEnd:
                        depth--;
                        break;
                    case (char)Chars.Assignment:
                        if (depth == 0)
                        {
                            return true;
                        }
                        break;
                }
            }

            return false;
        }

        private object ReadNext()
        {
            object? value = _scanner.SkipInsignificants().Peek() switch
            {
                Chars.StringBegin => ReadString(),
                Chars.ArrayBegin => ReadArray(),
                Chars.ObjectBegin => ReadObject(),
                _ => ReadBareValue(),
            };
            return value;
        }

        private string ReadString()
        {
            _scanner.Expect(Chars.StringBegin);
            _sb.Clear();

            while (!_scanner.Eof() && _scanner.Peek() != Chars.StringEnd)
            {
                var chr = _scanner.Next();
                switch (chr)
                {
                    case Chars.Escape:
                        chr = _scanner.Next();
                        switch(chr)
                        {
                            case Chars.Escape:
                            case Chars.StringEnd:
                                _sb.Append((char)chr);
                                break;
                            case 'n':
                                _sb.Append('\n');
                                break;
                            case 'r':
                                _sb.Append('\r');
                                break;
                            case 't':
                                _sb.Append('\t');
                                break;
                            default:
                                throw _scanner.ParseError($"Invalid escaped char: {(char)chr}");
                        }
                        break;
                    default:
                        _sb.Append((char)chr);
                        break;
                }
            }
            _scanner.Expect(Chars.StringEnd);
            return _sb.ToString();
        }

        private string ReadStringOrBareValue()
        {
            if (_scanner.SkipInsignificants().Peek() == Chars.StringBegin)
            {
                return ReadString();
            }
            return ReadBareValue();
        }

        private string ReadBareValue()
        {
            _sb.Clear();

            while (!_scanner.Eof() && !Chars.IsReserved(_scanner.Peek()))
            {
                _sb.Append((char)_scanner.Next());
            }
            // Trim the edge whitespace: internal spaces are preserved ("Clean Code"), but the space
            // before a separator or '}' is not part of the value.
            return _sb.ToString().Trim();
        }

        private object[] ReadArray()
        {
            _scanner.Expect(Chars.ArrayBegin);

            if (_scanner.SkipInsignificants().Peek() == Chars.ArrayEnd)
            {
                _scanner.Next();
                return [];
            }

            List<object> result = [];

            result.Add(ReadNext());

            while (!_scanner.SkipInsignificants().Eof() && _scanner.Peek() != Chars.ArrayEnd)
            {
                _scanner.Expect(Chars.Separator);
                result.Add(ReadNext());
            }

            _scanner.Expect(Chars.ArrayEnd);

            return [.. result];
        }

        private object[] ReadBareArray()
        {
            List<object> result = [ReadNext()];

            while (!_scanner.SkipInsignificants().Eof())
            {
                _scanner.Expect(Chars.Separator);
                result.Add(ReadNext());
            }

            return [.. result];
        }

        private Dictionary<string,object> ReadObject()
        {
            _scanner.Expect(Chars.ObjectBegin);

            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            
            if (_scanner.SkipInsignificants().Peek() == Chars.ObjectEnd)
            {
                _scanner.Next();
                return result;
            }

            ReadKeyValue(result);

            while (!_scanner.SkipInsignificants().Eof() && _scanner.Peek() != Chars.ObjectEnd)
            {
                _scanner.Expect(Chars.Separator);
                ReadKeyValue(result);
            }

            _scanner.Expect(Chars.ObjectEnd);

            return result;
        }

        private Dictionary<string, object> ReadBareObject()
        {
            var result = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            ReadKeyValue(result);

            while (!_scanner.SkipInsignificants().Eof())
            {
                _scanner.Expect(Chars.Separator);
                ReadKeyValue(result);
            }

            return result;
        }

        private void ReadKeyValue(IDictionary<string, object> dict)
        {
            var key = ReadStringOrBareValue();
            if (key.Length == 0)
            {
                throw _scanner.ParseError("Invalid key / field name");
            }
            _scanner
                .SkipInsignificants()
                .Expect(Chars.Assignment);

            dict.Add(key, ReadNext());
        }
    }
}
