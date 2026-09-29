using System.Globalization;

namespace Curl.Kerberos;

/// <summary>
/// Locates a realm's KDCs as MIT's <c>locate_kdc.c</c> does: the realm's <c>kdc</c> entries
/// in <c>krb5.conf</c> when it has any; otherwise, when
/// <see cref="KerberosConfiguration.DnsLookupKdc" /> allows, the SRV records of
/// <c>_kerberos._udp.REALM</c> and then <c>_kerberos._tcp.REALM</c> (RFC 4120 section
/// 7.2.3.2), each ordered by priority and then weight (ADR-0160).
/// </summary>
/// <param name="configuration">The parsed <c>krb5.conf</c>.</param>
/// <param name="srvLookup">Looks up SRV records.</param>
public sealed class KerberosKdcLocator(KerberosConfiguration configuration, IKerberosSrvLookup srvLookup)
{
    /// <summary>The KDC port when a <c>kdc</c> entry names none (RFC 4120 section 7.2.3.1).</summary>
    public const int DefaultPort = 88;

    /// <summary>The port of an <c>https://</c> entry that names none.</summary>
    public const int HttpsPort = 443;

    private const string HttpsPrefix = "https://";

    /// <summary>Locates <paramref name="realm" />'s KDCs.</summary>
    /// <param name="realm">The realm, matched case-sensitively.</param>
    /// <param name="cancellationToken">Cancels the DNS lookups.</param>
    /// <returns>The KDCs in the order to try them; empty when none is found.</returns>
    /// <exception cref="KerberosConfigurationException">
    /// A <c>kdc</c> entry is malformed (<see cref="KerberosConfigurationError.InvalidKdcAddress" />).
    /// </exception>
    public async Task<IReadOnlyList<KerberosKdcAddress>> LocateAsync(string realm, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> entries = configuration.KdcEntries(realm);
        if (entries.Count > 0)
        {
            return [.. entries.Select(ParseKdcEntry)];
        }

        if (!configuration.DnsLookupKdc)
        {
            return [];
        }

        List<KerberosKdcAddress> found = [.. await LookUpAsync(realm, "_udp", KerberosKdcTransport.Udp, cancellationToken).ConfigureAwait(false)];
        found.AddRange(await LookUpAsync(realm, "_tcp", KerberosKdcTransport.Tcp, cancellationToken).ConfigureAwait(false));
        return found;
    }

    /// <summary>
    /// Parses one <c>kdc</c> entry: an optional <c>udp/</c>, <c>tcp/</c> or <c>https://</c>
    /// prefix, then <c>host</c>, <c>host:port</c>, <c>[address]</c>, <c>[address]:port</c> or
    /// a bare IPv6 address, then for <c>https://</c> an optional <c>/path</c>.
    /// </summary>
    /// <param name="entry">The entry as written.</param>
    /// <returns>The KDC it names.</returns>
    /// <exception cref="KerberosConfigurationException">The entry is malformed (<see cref="KerberosConfigurationError.InvalidKdcAddress" />).</exception>
    public static KerberosKdcAddress ParseKdcEntry(string entry)
    {
        if (entry.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            string rest = entry[HttpsPrefix.Length..];
            int slash = rest.IndexOf('/', StringComparison.Ordinal);
            string path = slash < 0 ? string.Empty : rest[(slash + 1)..];
            string hostAndPort = slash < 0 ? rest : rest[..slash];
            return ParseHostAndPort(entry, hostAndPort, KerberosKdcTransport.Https, HttpsPort) with { HttpsPath = path };
        }

        return entry switch
        {
            _ when entry.StartsWith("udp/", StringComparison.Ordinal) => ParseHostAndPort(entry, entry[4..], KerberosKdcTransport.Udp, DefaultPort),
            _ when entry.StartsWith("tcp/", StringComparison.Ordinal) => ParseHostAndPort(entry, entry[4..], KerberosKdcTransport.Tcp, DefaultPort),
            _ => ParseHostAndPort(entry, entry, KerberosKdcTransport.UdpOrTcp, DefaultPort),
        };
    }

    private static KerberosKdcAddress ParseHostAndPort(string entry, string address, KerberosKdcTransport transport, int defaultPort)
    {
        if (address.Length == 0 || address[0] == ':')
        {
            throw Invalid(entry);
        }

        (string host, string? port) = SplitHostAndPort(entry, address);
        if (host.Length == 0)
        {
            throw Invalid(entry);
        }

        return new KerberosKdcAddress(transport, host, port is null ? defaultPort : ParsePort(entry, port));
    }

    private static (string Host, string? Port) SplitHostAndPort(string entry, string address)
    {
        int close = address.IndexOf(']', StringComparison.Ordinal);
        if (address[0] == '[' && close > 0)
        {
            return SplitBracketedHostAndPort(entry, address, close);
        }

        if (address.Count(character => character == ':') > 1)
        {
            return (address, null);
        }

        int colon = address.IndexOf(':', StringComparison.Ordinal);
        return colon < 0 ? (address, null) : (address[..colon], address[(colon + 1)..]);
    }

    private static (string Host, string? Port) SplitBracketedHostAndPort(string entry, string address, int close)
    {
        string rest = address[(close + 1)..];
        return rest.Length == 0 ? (address[1..close], null)
            : rest[0] == ':' ? (address[1..close], rest[1..])
            : throw Invalid(entry);
    }

    private static int ParsePort(string entry, string port) =>
        int.TryParse(port, NumberStyles.None, CultureInfo.InvariantCulture, out int number)
        && number is > 0 and <= 65535
            ? number
            : throw Invalid(entry);

    private static KerberosConfigurationException Invalid(string entry) =>
        new(KerberosConfigurationError.InvalidKdcAddress, entry);

    private async Task<IEnumerable<KerberosKdcAddress>> LookUpAsync(
        string realm,
        string protocol,
        KerberosKdcTransport transport,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<KerberosSrvRecord> records = await srvLookup.LookUpAsync($"_kerberos.{protocol}.{realm}", cancellationToken).ConfigureAwait(false);
        return records
            .Select(record => (Record: record, Host: record.Target.TrimEnd('.')))
            .Where(candidate => candidate.Host.Length > 0)
            .OrderBy(candidate => candidate.Record.Priority)
            .ThenByDescending(candidate => candidate.Record.Weight)
            .Select(candidate => new KerberosKdcAddress(transport, candidate.Host, candidate.Record.Port));
    }
}
