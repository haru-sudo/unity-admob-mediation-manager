using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace AdMobMediationManager.Editor
{
    /// <summary>
    /// A small JSON reader/writer used to edit <c>Packages/manifest.json</c>.
    /// </summary>
    /// <remarks>
    /// Unity's <c>JsonUtility</c> cannot read dictionaries, and the manifest is a free-form object,
    /// so this class parses JSON into plain .NET values:
    /// objects become <see cref="JsonObject"/> (which keeps the original key order, so a rewritten
    /// manifest produces a minimal diff), arrays become <see cref="List{T}"/> of object,
    /// strings become <see cref="string"/>, numbers become <see cref="double"/>,
    /// and true/false/null become <see cref="bool"/> / null.
    /// </remarks>
    internal static class MiniJson
    {
        /// <summary>Parses <paramref name="json"/>. Throws <see cref="FormatException"/> on invalid input.</summary>
        public static object Parse(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var parser = new Parser(json);
            object value = parser.ReadValue();
            parser.SkipWhitespace();
            if (!parser.AtEnd)
            {
                throw parser.Error("Unexpected text after the JSON value");
            }

            return value;
        }

        /// <summary>
        /// Writes <paramref name="value"/> as JSON indented with two spaces (the style Unity uses
        /// for manifest.json), followed by a trailing newline.
        /// </summary>
        public static string Serialize(object value)
        {
            var sb = new StringBuilder();
            Write(sb, value, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object value, int depth)
        {
            switch (value)
            {
                case null:
                    sb.Append("null");
                    break;
                case string s:
                    WriteString(sb, s);
                    break;
                case bool b:
                    sb.Append(b ? "true" : "false");
                    break;
                case double d:
                    sb.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    break;
                case int i:
                    sb.Append(i.ToString(CultureInfo.InvariantCulture));
                    break;
                case JsonObject obj:
                    WriteObject(sb, obj, depth);
                    break;
                case List<object> list:
                    WriteArray(sb, list, depth);
                    break;
                default:
                    throw new ArgumentException("Cannot write a value of type " + value.GetType().Name + " as JSON");
            }
        }

        private static void WriteObject(StringBuilder sb, JsonObject obj, int depth)
        {
            if (obj.Count == 0)
            {
                sb.Append("{}");
                return;
            }

            sb.Append("{\n");
            int index = 0;
            foreach (KeyValuePair<string, object> pair in obj)
            {
                Indent(sb, depth + 1);
                WriteString(sb, pair.Key);
                sb.Append(": ");
                Write(sb, pair.Value, depth + 1);
                sb.Append(++index < obj.Count ? ",\n" : "\n");
            }

            Indent(sb, depth);
            sb.Append('}');
        }

        private static void WriteArray(StringBuilder sb, List<object> list, int depth)
        {
            if (list.Count == 0)
            {
                sb.Append("[]");
                return;
            }

            sb.Append("[\n");
            for (int i = 0; i < list.Count; i++)
            {
                Indent(sb, depth + 1);
                Write(sb, list[i], depth + 1);
                sb.Append(i + 1 < list.Count ? ",\n" : "\n");
            }

            Indent(sb, depth);
            sb.Append(']');
        }

        private static void Indent(StringBuilder sb, int depth)
        {
            sb.Append(' ', depth * 2);
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }

                        break;
                }
            }

            sb.Append('"');
        }

        /// <summary>A recursive-descent parser over a JSON string.</summary>
        private sealed class Parser
        {
            private readonly string text;
            private int pos;

            public Parser(string text)
            {
                this.text = text;
            }

            public bool AtEnd
            {
                get { return pos >= text.Length; }
            }

            public FormatException Error(string message)
            {
                return new FormatException(message + " at character " + pos.ToString(CultureInfo.InvariantCulture));
            }

            public void SkipWhitespace()
            {
                while (pos < text.Length && char.IsWhiteSpace(text[pos]))
                {
                    pos++;
                }
            }

            public object ReadValue()
            {
                SkipWhitespace();
                if (AtEnd)
                {
                    throw Error("Unexpected end of JSON");
                }

                char c = text[pos];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': ExpectWord("true"); return true;
                    case 'f': ExpectWord("false"); return false;
                    case 'n': ExpectWord("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                        {
                            return ReadNumber();
                        }

                        throw Error("Unexpected character '" + c + "'");
                }
            }

            private JsonObject ReadObject()
            {
                var obj = new JsonObject();
                pos++; // {
                SkipWhitespace();
                if (Peek() == '}')
                {
                    pos++;
                    return obj;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (Peek() != '"')
                    {
                        throw Error("Expected a property name");
                    }

                    string key = ReadString();
                    SkipWhitespace();
                    Expect(':');
                    obj[key] = ReadValue();
                    SkipWhitespace();
                    char next = Peek();
                    pos++;
                    if (next == '}')
                    {
                        return obj;
                    }

                    if (next != ',')
                    {
                        pos--;
                        throw Error("Expected ',' or '}'");
                    }
                }
            }

            private List<object> ReadArray()
            {
                var list = new List<object>();
                pos++; // [
                SkipWhitespace();
                if (Peek() == ']')
                {
                    pos++;
                    return list;
                }

                while (true)
                {
                    list.Add(ReadValue());
                    SkipWhitespace();
                    char next = Peek();
                    pos++;
                    if (next == ']')
                    {
                        return list;
                    }

                    if (next != ',')
                    {
                        pos--;
                        throw Error("Expected ',' or ']'");
                    }
                }
            }

            private string ReadString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd)
                    {
                        throw Error("Unterminated string");
                    }

                    char c = text[pos++];
                    if (c == '"')
                    {
                        return sb.ToString();
                    }

                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }

                    if (AtEnd)
                    {
                        throw Error("Unterminated escape sequence");
                    }

                    char e = text[pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (pos + 4 > text.Length)
                            {
                                throw Error("Incomplete \\u escape");
                            }

                            int code;
                            if (!int.TryParse(text.Substring(pos, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                            {
                                throw Error("Invalid \\u escape");
                            }

                            sb.Append((char)code);
                            pos += 4;
                            break;
                        default:
                            throw Error("Invalid escape '\\" + e + "'");
                    }
                }
            }

            private double ReadNumber()
            {
                int start = pos;
                while (pos < text.Length && "+-0123456789.eE".IndexOf(text[pos]) >= 0)
                {
                    pos++;
                }

                double value;
                if (!double.TryParse(text.Substring(start, pos - start), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    pos = start;
                    throw Error("Invalid number");
                }

                return value;
            }

            private void ExpectWord(string word)
            {
                if (string.CompareOrdinal(text, pos, word, 0, word.Length) != 0)
                {
                    throw Error("Expected '" + word + "'");
                }

                pos += word.Length;
            }

            private void Expect(char c)
            {
                if (Peek() != c)
                {
                    throw Error("Expected '" + c + "'");
                }

                pos++;
            }

            private char Peek()
            {
                return AtEnd ? '\0' : text[pos];
            }
        }
    }

    /// <summary>
    /// A JSON object that remembers the order in which keys were added, so that rewriting a file
    /// keeps its layout. Setting an existing key keeps its position.
    /// </summary>
    internal sealed class JsonObject : IEnumerable<KeyValuePair<string, object>>
    {
        private readonly List<string> order = new List<string>();
        private readonly Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);

        /// <summary>Number of properties.</summary>
        public int Count
        {
            get { return order.Count; }
        }

        /// <summary>Gets or sets a property. Getting a missing key returns null.</summary>
        public object this[string key]
        {
            get
            {
                object value;
                return values.TryGetValue(key, out value) ? value : null;
            }

            set
            {
                if (!values.ContainsKey(key))
                {
                    order.Add(key);
                }

                values[key] = value;
            }
        }

        /// <summary>Returns true when the object has a property named <paramref name="key"/>.</summary>
        public bool ContainsKey(string key)
        {
            return values.ContainsKey(key);
        }

        /// <summary>Removes a property. Returns false when it did not exist.</summary>
        public bool Remove(string key)
        {
            if (!values.Remove(key))
            {
                return false;
            }

            order.Remove(key);
            return true;
        }

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            foreach (string key in order)
            {
                yield return new KeyValuePair<string, object>(key, values[key]);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
