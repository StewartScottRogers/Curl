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
    private const int MaximumEntryCanonicalNames = 4;

    /// <summary>
    /// Gets the addresses the query's answer held, including those a failed decode read before it
    /// stopped, which curl's shared entry keeps (BL-958); empty when the exchange failed.
    /// </summary>
    public IReadOnlyList<System.Net.IPAddress> Addresses => Answer?.Addresses ?? [];

    /// <summary>
    /// Gets a value indicating whether curl counts the query as answered when it decides to print the
    /// decoded entry: a decoded answer, and also a failed exchange, whose decode curl never runs
    /// (<c>doh_resp_decode</c> is skipped, so its result stays <c>DOH_OK</c>).
    /// </summary>
    public bool CountsAsAnswered => Answer is not { Failure: not DnsMessageFailure.None };

    /// <summary>
    /// Reads the results as the one entry curl 8.21.0 decodes both answers into, A first: an answer
    /// that is <see cref="DnsMessageFailure.NoContent" /> only on its own decodes when an earlier
    /// answer, decoded or not, already put an address or a CNAME in the entry (measured, BL-958); and
    /// the entry's 24 addresses and 4 CNAMEs are shared, so a later answer keeps only what the earlier
    /// ones left room for (measured, BL-1153).
    /// </summary>
    /// <param name="results">The query results, in the order curl decodes them.</param>
    /// <returns>
    /// The results, each <see cref="DnsMessageFailure.NoContent" /> the entry fills turned to
    /// <see cref="DnsMessageFailure.None" /> and each answer cut to the room the entry had left.
    /// </returns>
    public static DohQueryResult[] AsOneEntry(IReadOnlyList<DohQueryResult> results)
    {
        var entryHasContent = false;
        var addressRoom = MaximumEntryAddresses;
        var canonicalNameRoom = MaximumEntryCanonicalNames;
        var shared = new DohQueryResult[results.Count];
        for (var index = 0; index < results.Count; index++)
        {
            var result = entryHasContent ? results[index].DecodedIntoAFilledEntry() : results[index];
            shared[index] = result.CutToRoom(addressRoom, canonicalNameRoom);
            entryHasContent |= results[index].PutContentInTheEntry;
            addressRoom -= shared[index].Addresses.Count;
            canonicalNameRoom -= shared[index].Answer?.CanonicalNames.Count ?? 0;
        }

        return shared;
    }

    private bool PutContentInTheEntry => Answer is { } answer && (answer.Addresses.Count > 0 || answer.CanonicalNames.Count > 0);

    private DohQueryResult DecodedIntoAFilledEntry() => Answer is { Failure: DnsMessageFailure.NoContent } empty
        ? this with { Answer = empty with { Failure = DnsMessageFailure.None } }
        : this;

    // curl's doh_store_a and doh_store_cname stop storing once the shared entry holds 24 addresses or 4 CNAMEs.
    private DohQueryResult CutToRoom(int addressRoom, int canonicalNameRoom) => Answer is { } answer
        ? this with { Answer = answer with { Addresses = [.. answer.Addresses.Take(addressRoom)], CanonicalNames = [.. answer.CanonicalNames.Take(canonicalNameRoom)] } }
        : this;
}
