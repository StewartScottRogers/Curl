namespace Curl.Networking;

/// <summary>
/// Which of the HTTPS (type 65) DoH query's trace lines <see cref="DohDnsResolver" /> writes, as
/// curl 8.22.0 gates them by <c>--trace-config</c> (ADR-0727's BL-2979 amendment): its <c>[DoH]</c>
/// lines are <c>infof</c> on the DoH sub-transfer, which is verbose only under the <c>doh</c>
/// feature, and its <c>[DNS] [HTTPS]</c> lines are <c>CURL_TRC_DNS</c> on that sub-transfer, which
/// also needs the <c>dns</c> feature.
/// </summary>
/// <param name="DohLines">Whether the <c>[DoH]</c> lines are written.</param>
/// <param name="DnsLines">Whether the <c>[DNS] [HTTPS]</c> lines are written.</param>
public readonly record struct DohHttpsRecordTrace(bool DohLines, bool DnsLines)
{
    /// <summary>Gets the gate that writes every line.</summary>
    public static DohHttpsRecordTrace All { get; } = new(true, true);
}
