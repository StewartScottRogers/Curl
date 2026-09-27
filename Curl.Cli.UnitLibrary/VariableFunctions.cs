using System.Globalization;
using System.Text;

namespace Curl.Cli;

/// <summary>
/// Applies the functions a <c>{{name:function:function}}</c> expansion names to a variable's bytes, left
/// to right, as curl 8.21.0's <c>varfunc</c> does: <c>trim</c> drops leading and trailing white space,
/// <c>json</c> quotes the bytes as a JSON string without the surrounding quotes, <c>url</c> percent-encodes
/// every byte but <c>A-Z a-z 0-9 - . _ ~</c>, <c>b64</c> encodes as base64 and <c>64dec</c> decodes base64,
/// giving <c>[64dec-fail]</c> for text curl's decoder refuses. Each function turns empty content into
/// empty content. A function name is matched only when a <c>:</c> or <c>}</c> follows it.
/// </summary>
/// <remarks>
/// Measured with the local curl 8.21.0 on 2026-09-27 through <c>--expand-data</c> against a loopback
/// server, and checked against <c>src/var.c</c> and <c>src/tool_writeout_json.c</c> at tag
/// <c>curl-8_21_0</c>.
/// </remarks>
internal static class VariableFunctions
{
    private const string UnreservedUrlCharacters = "-._~";

    /// <summary>The bytes <c>json</c> escapes by name, each matched with its letter in <see cref="NamedJsonEscapeLetters"/>.</summary>
    private const string NamedJsonEscapes = "\\\"\b\f\n\r\t";

    private const string NamedJsonEscapeLetters = "\\\"bfnrt";

    private static readonly string[] Names = ["trim", "json", "url", "b64", "64dec"];

    /// <summary>
    /// Applies the functions in <paramref name="functions"/>, which starts at the <c>:</c> after the
    /// variable name and runs past the first <c>}</c>, to <paramref name="content"/>, stopping at that <c>}</c>.
    /// </summary>
    /// <param name="functions">The text from the <c>:</c> after the name to the end of the value.</param>
    /// <param name="content">The variable's bytes; empty for a variable that is not set.</param>
    /// <param name="result">The bytes after the last function, when every function is known; otherwise empty.</param>
    /// <returns><see langword="true"/> when every function named is one curl knows.</returns>
    internal static bool TryApply(string functions, byte[] content, out byte[] result)
    {
        result = content;
        int position = 0;
        while (functions[position] != '}')
        {
            position++;
            int function = Array.FindIndex(Names, name => IsFunctionAt(functions, position, name));
            if (function < 0)
            {
                result = [];
                return false;
            }

            result = result.Length == 0 ? [] : Apply(function, result);
            position += Names[function].Length;
        }

        return true;
    }

    /// <summary>Reports whether <paramref name="name"/> is at <paramref name="position"/> with a <c>:</c> or <c>}</c> right after it, as curl's <c>FUNCMATCH</c> checks.</summary>
    private static bool IsFunctionAt(string functions, int position, string name)
    {
        int end = position + name.Length;
        return end < functions.Length
            && string.CompareOrdinal(functions, position, name, 0, name.Length) == 0
            && functions[end] is ':' or '}';
    }

    private static byte[] Apply(int function, byte[] content) => function switch
    {
        0 => Trim(content),
        1 => JsonQuote(content),
        2 => UrlEncode(content),
        3 => Encoding.ASCII.GetBytes(Convert.ToBase64String(content)),
        _ => DecodeBase64(content),
    };

    /// <summary>Drops leading and trailing bytes C's <c>isspace</c> accepts: space, tab, line feed, vertical tab, form feed and carriage return.</summary>
    private static byte[] Trim(byte[] content)
    {
        int start = 0;
        int end = content.Length;
        while (start < end && IsSpace(content[start]))
        {
            start++;
        }

        while (end > start && IsSpace(content[end - 1]))
        {
            end--;
        }

        return content[start..end];
    }

    private static bool IsSpace(byte value) => value is (byte)' ' or (>= (byte)'\t' and <= (byte)'\r');

    /// <summary>Quotes as curl's <c>jsonquoted</c> does: <c>\\ \" \b \f \n \r \t</c> by name, other bytes below 32 as <c>\u00xx</c>, the rest unchanged.</summary>
    private static byte[] JsonQuote(byte[] content)
    {
        StringBuilder quoted = new();
        foreach (byte value in content)
        {
            quoted.Append(JsonEscape(value));
        }

        return Encoding.Latin1.GetBytes(quoted.ToString());
    }

    private static string JsonEscape(byte value)
    {
        int named = NamedJsonEscapes.IndexOf((char)value, StringComparison.Ordinal);
        if (named >= 0)
        {
            return "\\" + NamedJsonEscapeLetters[named];
        }

        return value < 32
            ? string.Create(CultureInfo.InvariantCulture, $"\\u{value:x4}")
            : ((char)value).ToString();
    }

    /// <summary>Percent-encodes as <c>curl_easy_escape</c> does, with upper-case hexadecimal digits.</summary>
    private static byte[] UrlEncode(byte[] content)
    {
        StringBuilder encoded = new();
        foreach (byte value in content)
        {
            char character = (char)value;
            if (char.IsAsciiLetterOrDigit(character) || UnreservedUrlCharacters.Contains(character, StringComparison.Ordinal))
            {
                encoded.Append(character);
            }
            else
            {
                encoded.Append(CultureInfo.InvariantCulture, $"%{value:X2}");
            }
        }

        return Encoding.ASCII.GetBytes(encoded.ToString());
    }

    /// <summary>
    /// Decodes as curl does, reading the content only up to its first NUL byte, as curl reads it as a C
    /// string; text it refuses decodes to <c>[64dec-fail]</c>.
    /// </summary>
    private static byte[] DecodeBase64(byte[] content)
    {
        int nul = Array.IndexOf(content, (byte)0);
        ReadOnlySpan<byte> text = nul < 0 ? content : content.AsSpan(0, nul);
        return CurlBase64Decoder.TryDecode(text, out byte[] decoded) ? decoded : "[64dec-fail]"u8.ToArray();
    }
}
