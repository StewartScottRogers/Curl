using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Protocol.Rtsp;

/// <summary>
/// Writes an RTSP/1.0 request head byte for byte as curl 8.21.0 writes it (ADR-0169).
/// </summary>
/// <remarks>
/// The order is the request line, <c>CSeq</c>, <c>Session</c>, <c>Referer</c>,
/// <c>User-Agent</c>, <c>Authorization</c>, then the <c>-H</c> headers in command-line order.
/// A <c>-H</c> header of the same name suppresses <c>Referer</c>, <c>User-Agent</c> and
/// <c>Authorization</c>, and is sent in its own place among the <c>-H</c> headers (measured,
/// BL-591): <c>-H 'X-A: 1' -H 'User-Agent: mine' -e http://r/ -u u:p</c> sends <c>CSeq</c>,
/// <c>Referer</c>, <c>Authorization</c>, <c>X-A: 1</c>, <c>User-Agent: mine</c>. A <c>-H</c>
/// header naming <c>CSeq</c> is refused before the request is written
/// (<see cref="NamesCSeq" />). No <c>Host</c> or <c>Accept</c> is sent.
/// </remarks>
internal static class RtspRequestFormatter
{
    /// <summary>The <c>User-Agent</c> curl 8.21.0 sends when <c>-A</c> is not given.</summary>
    internal const string DefaultUserAgent = "curl/8.21.0";

    /// <summary>The exit 85 message for a <c>-H</c> header that names <c>CSeq</c>.</summary>
    internal const string CustomCSeqRefused = "CSeq cannot be set as a custom header.";

    /// <summary>
    /// Determines whether a <c>-H</c> header names <c>CSeq</c>, in any case and in any of its
    /// forms (<c>CSeq: 5</c>, <c>CSeq:</c>, <c>cseq;</c>), which curl 8.21.0 refuses after
    /// connecting with exit 85 and <see cref="CustomCSeqRefused" /> (measured, BL-591).
    /// </summary>
    /// <param name="options">The HTTP options carrying the <c>-H</c> headers.</param>
    /// <returns><see langword="true" /> when a <c>-H</c> header names <c>CSeq</c>.</returns>
    internal static bool NamesCSeq(HttpRequestOptions options) =>
        options.Headers.Any(entry => RtspCustomHeader.Parse(entry).Names("CSeq"));

    /// <summary>Writes the request head.</summary>
    /// <param name="method">The request method.</param>
    /// <param name="target">The request target: <c>*</c> for <c>OPTIONS</c>.</param>
    /// <param name="sequenceNumber">The <c>CSeq</c> value.</param>
    /// <param name="sessionId">The <c>Session</c> value, or <see langword="null" /> for none.</param>
    /// <param name="options">The HTTP options that reach the request: <c>-H</c>, <c>-A</c>, <c>-e</c>.</param>
    /// <param name="authorization">The <c>Authorization</c> value, or <see langword="null" /> for none.</param>
    /// <returns>The request head, Latin-1 encoded, ending with the blank line.</returns>
    internal static byte[] Format(
        RtspMethod method,
        string target,
        long sequenceNumber,
        string? sessionId,
        HttpRequestOptions options,
        string? authorization)
    {
        RtspCustomHeader[] customHeaders = [.. options.Headers.Select(entry => RtspCustomHeader.Parse(HeadText(entry, options)))];
        StringBuilder head = new();
        head.Append(method.Name).Append(' ').Append(target).Append(" RTSP/1.0\r\n");
        head.Append("CSeq: ").Append(sequenceNumber.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        AppendUnlessOverridden(head, customHeaders, "Session", sessionId);
        AppendUnlessOverridden(head, customHeaders, "Referer", HeadText(options.Referer, options));
        AppendUnlessOverridden(head, customHeaders, "User-Agent", HeadText(options.UserAgent, options) ?? DefaultUserAgent);
        AppendUnlessOverridden(head, customHeaders, "Authorization", authorization);
        foreach (RtspCustomHeader header in customHeaders)
        {
            if (header.SentLine is { } line)
            {
                head.Append(line).Append("\r\n");
            }
        }

        head.Append("\r\n");
        return Encoding.Latin1.GetBytes(head.ToString());
    }

    [return: NotNullIfNotNull(nameof(text))]
    private static string? HeadText(string? text, HttpRequestOptions options) =>
        text is null ? null : Encoding.Latin1.GetString(options.CommandLineTextEncoding.GetBytes(text));

    private static void AppendUnlessOverridden(StringBuilder head, RtspCustomHeader[] customHeaders, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value) && !customHeaders.Any(header => header.Names(name)))
        {
            head.Append(name).Append(": ").Append(value).Append("\r\n");
        }
    }
}
