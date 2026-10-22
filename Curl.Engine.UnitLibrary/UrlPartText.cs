using System.Buffers;
using System.Globalization;
using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The transforms upstream's <c>curl_url_get</c> applies to a part (curl 8.22.0's
/// <c>lib/urlapi.c</c>): <c>CURLU_URLDECODE</c>, <c>CURLU_URLENCODE</c>, <c>CURLU_PUNYCODE</c> and
/// <c>CURLU_PUNY2IDN</c>, in that order of precedence (ADR-0601).
/// </summary>
internal static class UrlPartText
{
    private const string HexDigits = "0123456789ABCDEF";

    private const byte Space = (byte)' ';

    private const byte Delete = 0x7f;

    private const string PunycodePrefix = "xn--";

    private static readonly IdnMapping Idn = new();

    /// <summary>The bytes <c>curl_easy_escape</c> leaves as they are: RFC 3986's unreserved characters.</summary>
    private static readonly SearchValues<byte> Unreserved =
        SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~"u8);

    /// <summary>
    /// Applies the flags to a part's text: decoded under <c>CURLU_URLDECODE</c> (a query's <c>+</c>
    /// first becoming a space), else encoded under <c>CURLU_URLENCODE</c>, else, for the host only,
    /// converted to or from punycode.
    /// </summary>
    /// <param name="text">The part as the handle holds it.</param>
    /// <param name="flags">The get's flags.</param>
    /// <param name="part">The part, which says whether it is the query or the host.</param>
    /// <returns>The text, or <see cref="CurlUrlCode.UrlDecode" /> or <see cref="CurlUrlCode.BadHostname" />.</returns>
    public static UrlPartResult Transform(string text, CurlUrlFlags flags, CurlUrlPart part)
    {
        bool query = part == CurlUrlPart.Query;
        if (flags.HasFlag(CurlUrlFlags.UrlDecode))
        {
            string? decoded = Decode(query ? text.Replace('+', ' ') : text);

            return decoded is null ? UrlPartResult.Failed(CurlUrlCode.UrlDecode) : UrlPartResult.Found(decoded);
        }

        if (flags.HasFlag(CurlUrlFlags.UrlEncode))
        {
            return UrlPartResult.Found(Encode(text, query));
        }

        return part == CurlUrlPart.Host ? Punycode(text, flags) : UrlPartResult.Found(text);
    }

    /// <summary>
    /// Converts a host under <c>CURLU_PUNYCODE</c> (a name that is not ASCII to punycode) or, without
    /// it, <c>CURLU_PUNY2IDN</c> (an ASCII name holding <c>xn--</c> to Unicode).
    /// </summary>
    /// <param name="host">The host.</param>
    /// <param name="flags">The get's flags.</param>
    /// <returns>The host, or <see cref="CurlUrlCode.BadHostname" /> when it does not convert.</returns>
    public static UrlPartResult Punycode(string host, CurlUrlFlags flags)
    {
        bool ascii = Ascii.IsValid(host);
        if (flags.HasFlag(CurlUrlFlags.Punycode))
        {
            return ascii ? UrlPartResult.Found(host) : Convert(Idn.GetAscii, host);
        }

        bool punycoded = ascii && host.Contains(PunycodePrefix, StringComparison.Ordinal);

        return flags.HasFlag(CurlUrlFlags.PunycodeToIdn) && punycoded
            ? Convert(Idn.GetUnicode, host)
            : UrlPartResult.Found(host);
    }

    /// <summary>
    /// Escapes every UTF-8 byte but the unreserved <c>A-Z a-z 0-9 - . _ ~</c> as <c>%XX</c>, as
    /// <c>curl_easy_escape</c> does.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The escaped text.</returns>
    public static string Escape(string text)
    {
        StringBuilder escaped = new();
        foreach (byte value in UrlByteText.ToBytes(text))
        {
            if (Unreserved.Contains(value))
            {
                escaped.Append((char)value);
            }
            else
            {
                AppendHex(escaped, value);
            }
        }

        return escaped.ToString();
    }

    /// <summary>
    /// Decodes <c>%XX</c> as upstream's <c>Curl_urldecode</c> with <c>REJECT_CTRL</c> does: a
    /// <c>%</c> without two hex digits stays as it is, and any byte below 0x20 fails.
    /// </summary>
    private static string? Decode(string text)
    {
        byte[] bytes = UrlByteText.ToBytes(text);
        List<byte> decoded = new(bytes.Length);
        for (int index = 0; index < bytes.Length; index++)
        {
            byte value = bytes[index];
            if (value == '%' && IsHexPair(bytes, index + 1))
            {
                value = (byte)((HexValue(bytes[index + 1]) << 4) | HexValue(bytes[index + 2]));
                index += 2;
            }

            if (value < Space)
            {
                return null;
            }

            decoded.Add(value);
        }

        return UrlByteText.ToText([.. decoded]);
    }

    private static bool IsHexPair(byte[] bytes, int index) =>
        index + 1 < bytes.Length && char.IsAsciiHexDigit((char)bytes[index]) && char.IsAsciiHexDigit((char)bytes[index + 1]);

    private static int HexValue(byte digit) => System.Convert.ToInt32(((char)digit).ToString(), 16);

    /// <summary>
    /// Encodes as upstream's <c>urlencode_str</c> does: a byte below 0x20 or from 0x7f up as
    /// <c>%XX</c>, and a space as <c>%20</c>, or as <c>+</c> in a query and after a <c>?</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="query">Whether the text is a query, where a space is <c>+</c> from the start.</param>
    /// <returns>The encoded text.</returns>
    public static string Encode(string text, bool query) =>
        UrlEncodeString(text, relative: true, query ? UrlEncodeQuery.Yes : UrlEncodeQuery.NotYet);

    /// <summary>
    /// Encodes as curl 8.22.0's <c>urlencode_str</c> does: unless <paramref name="relative" />, the
    /// text up to the first <c>/</c> or <c>?</c> after its <c>//</c> as it is; then a byte below 0x20 or
    /// from 0x7f up as <c>%XX</c>, a <c>%XX</c> escape with a lower-case digit upper cased, and a space
    /// as <c>%20</c>, or as <c>+</c> where <paramref name="query" /> says.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="relative">Whether the text is a part or a relative reference, with no scheme and host to keep.</param>
    /// <param name="query">Where a space is <c>+</c>.</param>
    /// <returns>The encoded text.</returns>
    public static string UrlEncodeString(string text, bool relative, UrlEncodeQuery query)
    {
        byte[] bytes = UrlByteText.ToBytes(text);
        int start = relative ? 0 : HostEnd(bytes);
        StringBuilder encoded = new(UrlByteText.ToText(bytes.AsSpan(0, start)));
        for (int index = start; index < bytes.Length; index++)
        {
            index += AppendEncodedByte(encoded, bytes, index, ref query);
        }

        return encoded.ToString();
    }

    /// <summary>Appends the byte at <paramref name="index" /> as <c>urlencode_str</c> writes it.</summary>
    /// <returns>How many bytes after it were written too: 2 for an upper-cased escape, else 0.</returns>
    private static int AppendEncodedByte(StringBuilder encoded, byte[] bytes, int index, ref UrlEncodeQuery query)
    {
        byte value = bytes[index];
        if (value == Space)
        {
            encoded.Append(SpaceText(query));
            return 0;
        }

        if (value < Space || value >= Delete)
        {
            AppendHex(encoded, value);
            return 0;
        }

        if (IsLowerCaseEscape(bytes, index))
        {
            AppendHex(encoded, (byte)System.Convert.ToInt32(Encoding.ASCII.GetString(bytes, index + 1, 2), 16));
            return 2;
        }

        encoded.Append((char)value);
        query = QueryAfter(value, query);
        return 0;
    }

    /// <summary>Upstream's <c>find_host_sep</c>: the index of the first <c>/</c> or <c>?</c> after the <c>//</c>, else the end.</summary>
    private static int HostEnd(byte[] bytes)
    {
        int slashes = bytes.AsSpan().IndexOf("//"u8);
        int host = slashes < 0 ? 0 : slashes + 2;
        int end = bytes.AsSpan(host).IndexOfAny((byte)'/', (byte)'?');
        return end < 0 ? bytes.Length : host + end;
    }

    private static string SpaceText(UrlEncodeQuery query) => query == UrlEncodeQuery.Yes ? "+" : "%20";

    /// <summary>The query state after <paramref name="value" />: a <c>?</c> turns <see cref="UrlEncodeQuery.NotYet" /> into <see cref="UrlEncodeQuery.Yes" />.</summary>
    private static UrlEncodeQuery QueryAfter(byte value, UrlEncodeQuery query) =>
        value == '?' && query == UrlEncodeQuery.NotYet ? UrlEncodeQuery.Yes : query;

    private static bool IsLowerCaseEscape(byte[] bytes, int index) =>
        bytes[index] == '%'
        && index + 2 < bytes.Length
        && char.IsAsciiHexDigit((char)bytes[index + 1])
        && char.IsAsciiHexDigit((char)bytes[index + 2])
        && (char.IsAsciiLetterLower((char)bytes[index + 1]) || char.IsAsciiLetterLower((char)bytes[index + 2]));

    private static void AppendHex(StringBuilder text, byte value) =>
        text.Append('%').Append(HexDigits[value >> 4]).Append(HexDigits[value & 0xf]);

    private static UrlPartResult Convert(Func<string, string> conversion, string host)
    {
        try
        {
            return UrlPartResult.Found(conversion(host));
        }
        catch (ArgumentException)
        {
            return UrlPartResult.Failed(CurlUrlCode.BadHostname);
        }
    }
}
