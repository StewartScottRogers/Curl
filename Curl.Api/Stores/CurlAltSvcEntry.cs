using System.Globalization;

namespace Curl.Api;

/// <summary>
/// One entry of an alt-svc cache file: the origin's protocol, host and port, the alternative's
/// protocol, host and port, the expiry (<c>YYYYMMDD HH:MM:SS</c>), whether it persists across network
/// changes and its priority, in the order curl writes them on one line.
/// </summary>
/// <param name="SourceAlpn">The origin's ALPN id, such as <c>h2</c>.</param>
/// <param name="SourceHost">The origin's host name.</param>
/// <param name="SourcePort">The origin's port.</param>
/// <param name="DestinationAlpn">The alternative's ALPN id, such as <c>h3</c>.</param>
/// <param name="DestinationHost">The alternative's host name.</param>
/// <param name="DestinationPort">The alternative's port.</param>
/// <param name="Expire">When the entry expires, <c>YYYYMMDD HH:MM:SS</c> in UTC.</param>
/// <param name="Persist">Whether the entry survives a network change.</param>
/// <param name="Priority">The entry's priority; curl writes 0.</param>
public sealed record CurlAltSvcEntry(
    string SourceAlpn,
    string SourceHost,
    int SourcePort,
    string DestinationAlpn,
    string DestinationHost,
    int DestinationPort,
    string Expire,
    bool Persist,
    int Priority)
{
    /// <summary>The entry as one alt-svc cache file line: <c>h2 a.example 443 h3 b.example 443 "20261231 00:00:00" 0 0</c>.</summary>
    /// <returns>The line.</returns>
    public string ToFileLine() =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{SourceAlpn} {SourceHost} {SourcePort} {DestinationAlpn} {DestinationHost} {DestinationPort} \"{Expire}\" {(Persist ? 1 : 0)} {Priority}");

    /// <summary>The entry of one alt-svc cache file line, or <see langword="null"/> for a line that is not ten space-separated fields with numeric ports, persist and priority, as curl skips it.</summary>
    /// <param name="line">The line.</param>
    /// <returns>The entry, or <see langword="null"/>.</returns>
    public static CurlAltSvcEntry? FromFileLine(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        string[] fields = line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (fields.Length != 10
            || !int.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out int sourcePort)
            || !int.TryParse(fields[5], NumberStyles.None, CultureInfo.InvariantCulture, out int destinationPort)
            || !int.TryParse(fields[8], NumberStyles.None, CultureInfo.InvariantCulture, out int persist)
            || !int.TryParse(fields[9], NumberStyles.None, CultureInfo.InvariantCulture, out int priority))
        {
            return null;
        }

        string expire = $"{fields[6]} {fields[7]}".Trim('"');
        return new CurlAltSvcEntry(fields[0], fields[1], sourcePort, fields[3], fields[4], destinationPort, expire, persist != 0, priority);
    }
}
