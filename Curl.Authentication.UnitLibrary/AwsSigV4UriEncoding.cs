using System.Text;

namespace Curl.Authentication;

/// <summary>
/// Builds the canonical URI and canonical query string of a Signature Version 4 request, and
/// escapes the access key ID, byte for byte as curl 8.21.0's <c>http_aws_sigv4.c</c>.
/// </summary>
internal static class AwsSigV4UriEncoding
{
    /// <summary>
    /// The number of non-empty query components at which curl gives up with
    /// <c>CURLE_TOO_LARGE</c>.
    /// </summary>
    internal const int MaxQueryComponents = 128;

    /// <summary>
    /// Builds the canonical URI: the path as given for S3 (<c>s3</c>, <c>s3-express</c>,
    /// <c>s3-outposts</c>), otherwise every byte but an unreserved one or <c>/</c>
    /// percent-encoded again; <c>/</c> when empty.
    /// </summary>
    /// <param name="path">The URL's path.</param>
    /// <param name="service">The signing service.</param>
    /// <param name="encoding">The encoding that turns the path into bytes.</param>
    /// <returns>The canonical URI.</returns>
    internal static string CanonicalPath(string path, string service, Encoding encoding)
    {
        string canonical = service is "s3" or "s3-express" or "s3-outposts"
            ? path
            : Escape(encoding.GetBytes(path), keepSlash: true);
        return canonical.Length == 0 ? "/" : canonical;
    }

    /// <summary>
    /// Builds the canonical query string: the non-empty <c>&amp;</c>-separated components, each
    /// key and value decoded and re-encoded (a decoded <c>+</c> stays <c>%2B</c>, a literal
    /// <c>+</c> becomes <c>%20</c>), sorted by key then value, every value written as
    /// <c>key=value</c> or <c>key=</c>.
    /// </summary>
    /// <param name="query">The URL's query, or <see langword="null" />.</param>
    /// <param name="encoding">The encoding that turns the query into bytes.</param>
    /// <returns>The canonical query string, or <see langword="null" /> when the query has
    /// <see cref="MaxQueryComponents" /> components or more.</returns>
    internal static string? CanonicalQuery(string? query, Encoding encoding)
    {
        string[] components = (query ?? string.Empty).Split('&', StringSplitOptions.RemoveEmptyEntries);
        if (components.Length >= MaxQueryComponents)
        {
            return null;
        }

        IEnumerable<(string Key, string Value)> pairs = components
            .Select(component => EncodeComponent(component, encoding))
            .Order(QueryPairComparer.Instance);
        return string.Join('&', pairs.Select(pair => (pair.Key.Length == 0 ? EmptyKey : pair.Key) + "=" + pair.Value));
    }

    /// <summary>
    /// What curl writes for an empty key (<c>=z</c>): its key buffer is never allocated, and
    /// curl's printf writes a null <c>%s</c> as <c>(nil)</c>.
    /// </summary>
    private const string EmptyKey = "(nil)";

    /// <summary>
    /// Percent-encodes every byte but an ASCII letter, digit, <c>-</c>, <c>.</c>, <c>_</c> or
    /// <c>~</c>, as <c>curl_escape</c> does.
    /// </summary>
    /// <param name="text">The text to escape.</param>
    /// <param name="encoding">The encoding that turns the text into bytes.</param>
    /// <returns>The escaped text.</returns>
    internal static string EscapeText(string text, Encoding encoding) => Escape(encoding.GetBytes(text), keepSlash: false);

    private static (string Key, string Value) EncodeComponent(string component, Encoding encoding)
    {
        int equals = component.IndexOf('=', StringComparison.Ordinal);
        return equals < 0
            ? (NormalizeQueryPart(component, encoding), string.Empty)
            : (NormalizeQueryPart(component[..equals], encoding), NormalizeQueryPart(component[(equals + 1)..], encoding));
    }

    private static string NormalizeQueryPart(string part, Encoding encoding)
    {
        byte[] bytes = encoding.GetBytes(part);
        StringBuilder normalized = new();
        int index = 0;
        while (index < bytes.Length)
        {
            if (IsPercentEscape(bytes, index))
            {
                byte decoded = Convert.FromHexString(Encoding.ASCII.GetString(bytes, index + 1, 2))[0];
                normalized.Append(decoded == '+' ? "%2B" : EscapeQueryByte(decoded));
                index += 3;
            }
            else
            {
                normalized.Append(EscapeQueryByte(bytes[index]));
                index++;
            }
        }

        return normalized.ToString();
    }

    private static bool IsPercentEscape(byte[] bytes, int index) =>
        bytes[index] == '%' && index + 2 < bytes.Length && char.IsAsciiHexDigit((char)bytes[index + 1]) && char.IsAsciiHexDigit((char)bytes[index + 2]);

    private static string EscapeQueryByte(byte value) =>
        value == '+' ? "%20" : EscapeByte(value, keepSlash: false);

    private static string Escape(byte[] bytes, bool keepSlash) =>
        string.Concat(bytes.Select(value => EscapeByte(value, keepSlash)));

    private static string EscapeByte(byte value, bool keepSlash) =>
        IsUnreserved(value) || (keepSlash && value == '/') ? ((char)value).ToString() : "%" + value.ToString("X2", System.Globalization.CultureInfo.InvariantCulture);

    private static bool IsUnreserved(byte value) =>
        char.IsAsciiLetterOrDigit((char)value) || value is (byte)'-' or (byte)'.' or (byte)'_' or (byte)'~';

    /// <summary>
    /// Orders query pairs as curl's <c>compare_func</c>: by key, then by value, bytewise, an empty
    /// key or value first; two empty keys compare equal whatever their values.
    /// </summary>
    private sealed class QueryPairComparer : IComparer<(string Key, string Value)>
    {
        internal static readonly QueryPairComparer Instance = new();

        public int Compare((string Key, string Value) x, (string Key, string Value) y)
        {
            if (x.Key.Length == 0 || y.Key.Length == 0)
            {
                return Math.Sign(x.Key.Length) - Math.Sign(y.Key.Length);
            }

            int byKey = string.CompareOrdinal(x.Key, y.Key);
            return byKey != 0 ? byKey : string.CompareOrdinal(x.Value, y.Value);
        }
    }
}
