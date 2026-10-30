using System.Globalization;
using Curl.Extension.DnsQuery.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.DnsQuery.Transport.Https;

/// <summary>
/// The status line and the headers of a DoH server's HTTP/1.1 response that say how to read its body (ADR-1148).
/// </summary>
/// <param name="StatusCode">The status code.</param>
/// <param name="ContentType">The <c>Content-Type</c> header's value, or <see langword="null" /> when there is none.</param>
/// <param name="ContentLength">The <c>Content-Length</c>, or <see langword="null" /> when there is none.</param>
/// <param name="IsChunked">Whether <c>Transfer-Encoding</c> is <c>chunked</c>.</param>
internal sealed record DohResponseHead(int StatusCode, string? ContentType, int? ContentLength, bool IsChunked)
{
    /// <summary>The longest DNS message, the most a DoH body may hold.</summary>
    public const int MaxMessageLength = ushort.MaxValue;

    private const int Ok = 200;

    /// <summary>Gets whether the status is informational (1xx), a response that another follows.</summary>
    public bool IsInterim => StatusCode is >= 100 and < 200;

    /// <summary>Reads the head from its lines, the status line first.</summary>
    /// <param name="lines">The head's lines without their line ends, the empty line that ends it excluded.</param>
    /// <returns>The head.</returns>
    /// <exception cref="DnsQueryException">The status line or a header is malformed (exit 8).</exception>
    public static DohResponseHead Parse(IReadOnlyList<string> lines)
    {
        string[] status = lines[0].Split(' ', 3);
        if (status.Length < 2 || !status[0].StartsWith("HTTP/1.", StringComparison.Ordinal) || status[1].Length != 3
            || !int.TryParse(status[1], NumberStyles.None, CultureInfo.InvariantCulture, out int code))
        {
            throw Malformed($"The DoH server's status line \"{lines[0]}\" is not HTTP/1.x");
        }

        return lines.Skip(1).Aggregate(new DohResponseHead(code, null, null, false), WithHeader);
    }

    /// <summary>Checks that the response carries a DNS message (RFC 8484 section 4.2.1).</summary>
    /// <exception cref="DnsQueryException">
    /// The status is not 200 (exit 22, as curl's <c>--fail</c>), or the content type is not
    /// <c>application/dns-message</c> (exit 8).
    /// </exception>
    public void EnsureDnsMessage()
    {
        if (StatusCode != Ok)
        {
            throw new DnsQueryException(CurlExitCode.HttpReturnedError, $"The DoH server answered HTTP {StatusCode.ToString(CultureInfo.InvariantCulture)}");
        }

        string mediaType = (ContentType ?? string.Empty).Split(';')[0].Trim();
        if (!mediaType.Equals(DohRequest.MediaType, StringComparison.OrdinalIgnoreCase))
        {
            throw Malformed($"The DoH server answered with content type \"{ContentType}\", not {DohRequest.MediaType}");
        }
    }

    /// <summary>Gives the failure a malformed response ends with (exit 8).</summary>
    /// <param name="message">What is malformed.</param>
    /// <returns>The failure.</returns>
    public static DnsQueryException Malformed(string message) => new(CurlExitCode.WeirdServerReply, message);

    private static DohResponseHead WithHeader(DohResponseHead head, string line)
    {
        int colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 0)
        {
            throw Malformed($"The DoH server's header line \"{line}\" has no name");
        }

        string value = line[(colon + 1)..].Trim();
        return line[..colon].Trim().ToUpperInvariant() switch
        {
            "CONTENT-TYPE" => head with { ContentType = value },
            "CONTENT-LENGTH" => head with { ContentLength = LengthOf(value) },
            "TRANSFER-ENCODING" => head with { IsChunked = value.Equals("chunked", StringComparison.OrdinalIgnoreCase) },
            _ => head,
        };
    }

    private static int LengthOf(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int length) && length <= MaxMessageLength
            ? length
            : throw Malformed($"The DoH server's Content-Length \"{value}\" is not a DNS message's length");
}
