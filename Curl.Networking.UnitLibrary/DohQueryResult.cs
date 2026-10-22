using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>What one of <see cref="DohDnsResolver" />'s two DoH queries came back with.</summary>
/// <param name="RecordType">The type asked for: <see cref="DnsRecordType.A" />, <see cref="DnsRecordType.Aaaa" /> or <see cref="DnsRecordType.Https" />.</param>
/// <param name="ExchangeFailure">
/// <see cref="CurlExitCode.Ok" /> when the response was read; otherwise the exit code the connection
/// or the exchange failed with, and then <paramref name="Answer" /> is <see langword="null" />.
/// </param>
/// <param name="Answer">The decoded answer, or <see langword="null" /> when the exchange failed.</param>
internal sealed record DohQueryResult(DnsRecordType RecordType, CurlExitCode ExchangeFailure, DnsAnswer? Answer)
{
    private const int MaximumEntryAddresses = 24;

    /// <summary>
    /// Gets the addresses the query's answer held, including those a failed decode read before it
    /// stopped, which curl's shared entry keeps (BL-958); empty when the exchange failed.
    /// </summary>
    public IReadOnlyList<System.Net.IPAddress> Addresses => Answer?.Addresses ?? [];

    /// <summary>
    /// Gets a value indicating whether curl counts the query as answered when it takes the entry's
    /// addresses: a decoded answer, and also a failed exchange, whose decode curl never runs
    /// (<c>doh_resp_decode</c> is skipped, so its result stays <c>DOH_OK</c>).
    /// </summary>
    public bool CountsAsAnswered => Answer is not { Failure: not DnsMessageFailure.None };

    /// <summary>
    /// Reads the results as the one entry curl decodes both answers into, A first: the entry's 24
    /// addresses are shared, so a later answer keeps only what the earlier ones left room for
    /// (measured, BL-1153).
    /// </summary>
    /// <param name="results">The query results, in the order curl decodes them.</param>
    /// <returns>The results, each answer cut to the room the entry had left.</returns>
    public static DohQueryResult[] AsOneEntry(IReadOnlyList<DohQueryResult> results)
    {
        var addressRoom = MaximumEntryAddresses;
        var shared = new DohQueryResult[results.Count];
        for (var index = 0; index < results.Count; index++)
        {
            shared[index] = results[index].CutToRoom(addressRoom);
            addressRoom -= shared[index].Addresses.Count;
        }

        return shared;
    }

    // curl's doh_store_a stops storing once the shared entry holds 24 addresses.
    private DohQueryResult CutToRoom(int addressRoom) => Answer is { } answer
        ? this with { Answer = answer with { Addresses = [.. answer.Addresses.Take(addressRoom)] } }
        : this;
}
