namespace Curl.Networking;

/// <summary>
/// Writes the line curl 8.22.0's HTTPSRR build prints under <c>-v</c> once an <c>https://</c>
/// origin's HTTPS-RR resolve has finished, derived from <c>cf_dns_report</c> in
/// <c>lib/vdns/cf-dns.c</c> and <c>Curl_httpsrr_applicable</c> in <c>lib/vdns/httpsrr.c</c> at tag
/// <c>curl-8_22_0</c> (ADR-0727's BL-2980 amendment): <c>HTTPS-RR &lt;host&gt;:&lt;port&gt;: -</c>
/// with no record, <c>not applicable</c> when the record names another target or another port, and
/// otherwise the record as <c>Curl_httpsrr_print</c> writes it.
/// </summary>
internal static class HttpsRecordReportLine
{
    // The targets Curl_httpsrr_applicable reads as the origin itself.
    private static readonly string[] OriginTargets = ["", "."];

    /// <summary>Formats the line for <paramref name="host" />'s HTTPS-RR resolve.</summary>
    /// <param name="host">The origin's host name.</param>
    /// <param name="port">The origin's port.</param>
    /// <param name="record">The first HTTPS record the resolve decoded, or <see langword="null" /> for none.</param>
    /// <returns>The line, without curl's <c>* </c> prefix.</returns>
    public static string Format(string host, int port, ServiceBindingRecord? record)
    {
        var prefix = $"HTTPS-RR {host}:{port}: ";
        if (record is null)
        {
            return prefix + "-";
        }

        return IsApplicable(record, port)
            ? prefix + DohTraceLines.FormatHttpsRecord(record, hasMandatoryKeys: false)
            : prefix + "not applicable";
    }

    // Curl_httpsrr_applicable: the record's target is the origin itself (empty or ".") and it names
    // no port or the origin's.
    private static bool IsApplicable(ServiceBindingRecord record, int port) =>
        OriginTargets.Contains(record.TargetName) && (record.Port ?? port) == port;
}
