using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Curl.Networking;

// The lines for what the A and AAAA answers decoded to: the failures and the one entry.
internal static partial class DohTraceLines
{
    /// <summary>
    /// Formats an address as curl's <c>[DoH]</c> lines do: dotted for IPv4, and for IPv6 all eight
    /// groups as four lower-case hex digits, uncompressed (<c>0000:...:0001</c> for <c>::1</c>).
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>The text after <c>[DoH] A: </c> or <c>[DoH] AAAA: </c>.</returns>
    internal static string FormatAddress(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        return string.Join(':', Enumerable.Range(0, 8).Select(group => ((bytes[2 * group] << 8) | bytes[(2 * group) + 1]).ToString("x4", CultureInfo.InvariantCulture)));
    }

    // curl keeps one entry for both queries, so the TTL is the smallest either answer read and the
    // addresses and names come A's first.
    private static IEnumerable<string> DecodedEntryLines(string host, IReadOnlyList<DohQueryResult> results)
    {
        var answers = results.Select(result => result.Answer).OfType<DnsAnswer>().ToList();
        return
        [
            $"[DNS] hostname: {host}",
            $"[DoH] TTL: {SmallestTimeToLive(answers)} seconds",
            .. results.SelectMany(AddressLines),
            .. answers.SelectMany(answer => answer.CanonicalNames).Select(name => $"CNAME: {name}"),
        ];
    }

    private static uint SmallestTimeToLive(List<DnsAnswer> answers) =>
        answers.Select(answer => answer.TimeToLiveSeconds).DefaultIfEmpty((uint)int.MaxValue).Min();

    private static IEnumerable<string> AddressLines(DohQueryResult result) =>
        result.Addresses.Select(address => $"[DoH] {TypeName(result.RecordType)}: {FormatAddress(address)}");

    private static string DecodeFailureLine(string host, DohQueryResult failed) =>
        $"[DNS] DoH: {DnsMessageFailureText.Describe(failed.Answer!.Failure)} type {TypeName(failed.RecordType)} for {host}";

    private static string TypeName(DnsRecordType recordType) => recordType == DnsRecordType.A ? "A" : "AAAA";
}
