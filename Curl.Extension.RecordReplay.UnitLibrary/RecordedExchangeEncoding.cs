using System.Globalization;
using System.Text;

namespace Curl.Extension.RecordReplay;

/// <summary>
/// Encodes and decodes an exchange file (ADR-1135 section 3): the line <c>CURL-EXCHANGE 1</c>, the line
/// <c>&lt;method&gt; &lt;url&gt;</c>, the line <c>&lt;status&gt; &lt;head length&gt; &lt;body length&gt;</c>, each ended by
/// one LF, then the head bytes and the body bytes. Lines are Latin-1; nothing follows the body.
/// </summary>
public static class RecordedExchangeEncoding
{
    private const string Magic = "CURL-EXCHANGE 1";

    /// <summary>Encodes <paramref name="exchange" /> as an exchange file.</summary>
    public static byte[] Encode(RecordedExchange exchange)
    {
        ArgumentNullException.ThrowIfNull(exchange);
        var lines = Encoding.Latin1.GetBytes(string.Create(
            CultureInfo.InvariantCulture,
            $"{Magic}\n{exchange.Method} {exchange.Url}\n{exchange.StatusCode} {exchange.Head.Length} {exchange.Body.Length}\n"));
        return [.. lines, .. exchange.Head, .. exchange.Body];
    }

    /// <summary>Decodes an exchange file; <see langword="null" /> when it is not one, or is truncated or overlong.</summary>
    public static RecordedExchange? Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        var text = Encoding.Latin1.GetString(bytes);
        var lines = text.Split('\n', 4);
        if (lines.Length < 4 || lines[0] != Magic)
        {
            return null;
        }

        var request = lines[1].Split(' ', 2);
        var lengths = lines[2].Split(' ');
        return request.Length == 2 && lengths.Length == 3 && TryParse(lengths, out var status, out var headLength, out var bodyLength)
            ? Exchange(request, status, bytes.AsSpan(bytes.Length - lines[3].Length), headLength, bodyLength)
            : null;
    }

    private static bool TryParse(string[] fields, out int status, out int headLength, out int bodyLength)
    {
        headLength = 0;
        bodyLength = 0;
        return int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out status)
            && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out headLength)
            && int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out bodyLength);
    }

    private static RecordedExchange? Exchange(string[] request, int status, ReadOnlySpan<byte> rest, int headLength, int bodyLength) =>
        (long)headLength + bodyLength == rest.Length
            ? new RecordedExchange(request[0], request[1], status, rest[..headLength].ToArray(), rest[headLength..].ToArray())
            : null;
}
