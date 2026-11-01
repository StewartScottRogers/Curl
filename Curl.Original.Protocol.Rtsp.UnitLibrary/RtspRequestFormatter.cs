using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Rtsp;

/// <summary>
/// Writes an RTSP/1.0 request head byte for byte as curl 8.21.0 writes it (ADR-0169).
/// </summary>
/// <remarks>
/// The order is the request line, <c>CSeq</c>, <c>Session</c>, <c>Range</c>, <c>Referer</c>,
/// <c>User-Agent</c>, <c>Authorization</c>, then the <c>-H</c> headers in command-line order.
/// A <c>-H</c> header of the same name suppresses <c>Range</c>, <c>Referer</c>,
/// <c>User-Agent</c> and <c>Authorization</c>, and is sent in its own place among the <c>-H</c> headers (measured,
/// BL-591): <c>-H 'X-A: 1' -H 'User-Agent: mine' -e http://r/ -u u:p</c> sends <c>CSeq</c>,
/// <c>Referer</c>, <c>Authorization</c>, <c>X-A: 1</c>, <c>User-Agent: mine</c>. A <c>-H</c>
/// header naming <c>CSeq</c> or <c>Session</c> is refused before the request is written
/// (<see cref="NamesCSeq" />, <see cref="NamesSession" />). A held session ID is sent even
/// when empty, as libcurl's <c>rtsp.c</c> sends any ID it holds. No <c>Host</c> or
/// <c>Accept</c> is sent.
/// </remarks>
internal static class RtspRequestFormatter
{
    /// <summary>The <c>User-Agent</c> curl 8.21.0 sends when <c>-A</c> is not given.</summary>
    internal const string DefaultUserAgent = "curl/8.22.0";

    /// <summary>The exit 85 message for a <c>-H</c> header that names <c>CSeq</c>.</summary>
    /// <summary>
    /// The <c>Accept-Encoding</c> value a libcurl <c>DESCRIBE</c> sends for <c>--compressed</c>:
    /// the four tokens the HTTP library sends, the same on the Schannel and OpenSSL builds (ADR-0287).
    /// </summary>
    internal const string AcceptEncoding = "deflate, gzip, br, zstd";

    /// <summary>The exit 85 message for a <c>-H</c> header that names <c>CSeq</c>.</summary>
    internal const string CustomCSeqRefused = "CSeq cannot be set as a custom header.";

    /// <summary>The exit 43 message for a <c>-H</c> header that names <c>Session</c>.</summary>
    internal const string CustomSessionRefused = "Session ID cannot be set as a custom header.";

    /// <summary>
    /// Determines whether a <c>-H</c> header names <c>CSeq</c>, in any case and in any of its
    /// forms (<c>CSeq: 5</c>, <c>CSeq:</c>, <c>cseq;</c>), which curl 8.21.0 refuses after
    /// connecting with exit 85 and <see cref="CustomCSeqRefused" /> (measured, BL-591).
    /// </summary>
    /// <param name="options">The HTTP options carrying the <c>-H</c> headers.</param>
    /// <returns><see langword="true" /> when a <c>-H</c> header names <c>CSeq</c>.</returns>
    internal static bool NamesCSeq(HttpRequestOptions options) =>
        options.Headers.Any(entry => RtspCustomHeader.Parse(entry).Names("CSeq"));

    /// <summary>
    /// Determines whether a <c>-H</c> header names <c>Session</c>, in any case and in any of
    /// its forms (<c>Session: 5</c>, <c>Session:</c>, <c>session;</c>), which curl 8.21.0
    /// refuses after connecting with exit 43 and <see cref="CustomSessionRefused" />, after the
    /// <c>CSeq</c> check (measured, BL-592).
    /// </summary>
    /// <param name="options">The HTTP options carrying the <c>-H</c> headers.</param>
    /// <returns><see langword="true" /> when a <c>-H</c> header names <c>Session</c>.</returns>
    internal static bool NamesSession(HttpRequestOptions options) =>
        options.Headers.Any(entry => RtspCustomHeader.Parse(entry).Names("Session"));

    /// <summary>
    /// Gives the <c>Range</c> value curl 8.21.0 sends for <c>-C</c> and <c>-r</c>, as
    /// <c>lib/url.c</c>'s <c>setup_range</c> builds it (BL-1293): a non-zero <c>-C &lt;n&gt;</c>
    /// gives <c>&lt;n&gt;-</c> and wins over <c>-r</c>; otherwise the <c>-r</c> text as typed,
    /// with no <c>bytes=</c>; <c>-C 0</c> alone gives none.
    /// </summary>
    /// <param name="resumeFrom">The <c>-C</c> offset, or <see langword="null" /> when not resuming.</param>
    /// <param name="rangeText">The <c>-r</c> text as typed, or <see langword="null" /> when not given.</param>
    /// <returns>The <c>Range</c> value, or <see langword="null" /> for none.</returns>
    internal static string? RangeValue(long? resumeFrom, string? rangeText) =>
        resumeFrom is { } offset and not 0
            ? offset.ToString(CultureInfo.InvariantCulture) + "-"
            : rangeText;

    /// <summary>Writes the request head.</summary>
    /// <param name="method">The request method.</param>
    /// <param name="target">The request target: <c>*</c> for <c>OPTIONS</c>.</param>
    /// <param name="sequenceNumber">The <c>CSeq</c> value.</param>
    /// <param name="sessionId">The <c>Session</c> value, sent even when empty, or <see langword="null" /> for none.</param>
    /// <param name="range">The <c>Range</c> value (<see cref="RangeValue" />), or <see langword="null" /> for none.</param>
    /// <param name="options">The HTTP options that reach the request: <c>-H</c>, <c>-A</c>, <c>-e</c>.</param>
    /// <param name="authorization">The <c>Authorization</c> value, or <see langword="null" /> for none.</param>
    /// <param name="plan">
    /// The libcurl request's kind, <c>Transport</c> and body (BL-2751), or <see langword="null" /> for
    /// the curl tool's <c>OPTIONS</c>, the one request that sends curl's default <c>User-Agent</c>.
    /// </param>
    /// <param name="timeCondition">The <c>-z</c> condition, or <see langword="null" /> for none.</param>
    /// <returns>The request head, Latin-1 encoded, ending with the blank line; the body is not included.</returns>
    /// <remarks>
    /// For a libcurl request, <c>lib/rtsp.c</c>'s order: <c>Transport</c> (a <c>SETUP</c> only),
    /// <c>Accept: application/sdp</c> and, for <c>--compressed</c>, <c>Accept-Encoding</c> (a
    /// <c>DESCRIBE</c> only) follow <c>Session</c>; the time condition (a <c>SETUP</c> or
    /// <c>DESCRIBE</c> only) follows <c>Authorization</c> (BL-2781); and a body's
    /// <c>Content-Length</c> and <c>Content-Type</c> follow the custom headers; a custom header of
    /// the same name suppresses each.
    /// </remarks>
    internal static byte[] Format(
        RtspMethod method,
        string target,
        long sequenceNumber,
        string? sessionId,
        string? range,
        HttpRequestOptions options,
        string? authorization,
        RtspRequestPlan? plan = null,
        TimeCondition? timeCondition = null)
    {
        plan ??= RtspRequestPlan.ToolTransfer;
        RtspCustomHeader[] customHeaders = [.. options.Headers.Select(entry => RtspCustomHeader.Parse(HeadText(entry, options)))];
        StringBuilder head = new();
        head.Append(method.Name).Append(' ').Append(target).Append(" RTSP/1.0\r\n");
        head.Append("CSeq: ").Append(sequenceNumber.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
        if (sessionId is not null)
        {
            head.Append("Session: ").Append(sessionId).Append("\r\n");
        }

        AppendRequestKindHeaders(head, customHeaders, plan, options.Compressed);
        AppendUnlessOverridden(head, customHeaders, "Range", HeadText(range, options));
        AppendUnlessOverridden(head, customHeaders, "Referer", HeadText(options.Referer, options));
        AppendUnlessOverridden(head, customHeaders, "User-Agent", UserAgentValue(options, plan));
        AppendUnlessOverridden(head, customHeaders, "Authorization", authorization);
        AppendTimeCondition(head, customHeaders, plan, timeCondition);
        AppendCustomHeaders(head, customHeaders);
        AppendBodyHeaders(head, customHeaders, plan);
        head.Append("\r\n");
        return Encoding.Latin1.GetBytes(head.ToString());
    }

    /// <summary>Gives the <c>-A</c> value, else curl's default for the tool's transfer only; libcurl sends none by default.</summary>
    private static string? UserAgentValue(HttpRequestOptions options, RtspRequestPlan plan) =>
        HeadText(options.UserAgent, options) ?? (plan.IsToolTransfer ? DefaultUserAgent : null);

    /// <summary>Writes a <c>SETUP</c>'s <c>Transport</c> and a <c>DESCRIBE</c>'s <c>Accept</c>.</summary>
    private static void AppendRequestKindHeaders(StringBuilder head, RtspCustomHeader[] customHeaders, RtspRequestPlan plan, bool compressed)
    {
        bool describes = plan.Kind == RtspRequestKind.Describe;
        AppendUnlessOverridden(head, customHeaders, "Transport", plan.TransportValue(NamesHeader(customHeaders, "Transport")));
        AppendUnlessOverridden(head, customHeaders, "Accept", describes ? "application/sdp" : null);
        AppendUnlessOverridden(head, customHeaders, "Accept-Encoding", describes && compressed ? AcceptEncoding : null);
    }

    /// <summary>
    /// Writes <c>If-Modified-Since</c> for <c>-z date</c> or <c>If-Unmodified-Since</c> for
    /// <c>-z -date</c> on a libcurl <c>SETUP</c> or <c>DESCRIBE</c> only, as <c>rtsp_do</c> calls
    /// <c>Curl_add_timecondition</c>, unless a custom header names it.
    /// </summary>
    private static void AppendTimeCondition(StringBuilder head, RtspCustomHeader[] customHeaders, RtspRequestPlan plan, TimeCondition? timeCondition)
    {
        if (timeCondition is null || plan.Kind is not (RtspRequestKind.Setup or RtspRequestKind.Describe))
        {
            return;
        }

        string name = timeCondition.Kind == TimeConditionKind.IfUnmodifiedSince ? "If-Unmodified-Since" : "If-Modified-Since";
        AppendUnlessOverridden(head, customHeaders, name, RtspConditionDate.Format(timeCondition));
    }

    private static void AppendCustomHeaders(StringBuilder head, RtspCustomHeader[] customHeaders)
    {
        foreach (RtspCustomHeader header in customHeaders)
        {
            if (header.SentLine is { } line)
            {
                head.Append(line).Append("\r\n");
            }
        }
    }

    /// <summary>
    /// Determines whether a <c>-H</c> header names <c>Transport</c>, which stands in for the
    /// <c>Transport</c> setting a <c>SETUP</c> needs.
    /// </summary>
    /// <param name="options">The HTTP options carrying the custom headers.</param>
    /// <returns><see langword="true" /> when a custom header names <c>Transport</c>.</returns>
    internal static bool NamesTransport(HttpRequestOptions options) =>
        options.Headers.Any(entry => RtspCustomHeader.Parse(entry).Names("Transport"));

    private static bool NamesHeader(RtspCustomHeader[] customHeaders, string name) =>
        customHeaders.Any(header => header.Names(name));

    /// <summary>Writes a body's <c>Content-Length</c> and <c>Content-Type</c>, unless a custom header names each.</summary>
    private static void AppendBodyHeaders(StringBuilder head, RtspCustomHeader[] customHeaders, RtspRequestPlan plan)
    {
        int length = plan.SentBody.Length;
        if (length > 0)
        {
            AppendUnlessOverridden(head, customHeaders, "Content-Length", length.ToString(CultureInfo.InvariantCulture));
            AppendUnlessOverridden(head, customHeaders, "Content-Type", plan.BodyContentType);
        }
    }

    [return: NotNullIfNotNull(nameof(text))]
    private static string? HeadText(string? text, HttpRequestOptions options) =>
        text is null ? null : WireText(text, options.CommandLineTextEncoding);

    // Text as its bytes in encoding, one character per byte, with each lone surrogate from U+DC80
    // to U+DCFF (an argument byte UTF-8 could not read, CGR-016) given as that byte.
    private static string WireText(string text, Encoding encoding)
    {
        StringBuilder wire = new(text.Length);
        int runStart = 0;
        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] is >= '\uDC80' and <= '\uDCFF' && !char.IsHighSurrogate(text[Math.Max(index - 1, 0)]))
            {
                wire.Append(Encoding.Latin1.GetString(encoding.GetBytes(text[runStart..index]))).Append((char)(text[index] - 0xDC00));
                runStart = index + 1;
            }
        }

        return wire.Append(Encoding.Latin1.GetString(encoding.GetBytes(text[runStart..]))).ToString();
    }

    private static void AppendUnlessOverridden(StringBuilder head, RtspCustomHeader[] customHeaders, string name, string? value)
    {
        if (!string.IsNullOrEmpty(value) && !customHeaders.Any(header => header.Names(name)))
        {
            head.Append(name).Append(": ").Append(value).Append("\r\n");
        }
    }
}
