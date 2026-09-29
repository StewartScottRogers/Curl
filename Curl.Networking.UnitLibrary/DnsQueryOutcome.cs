namespace Curl.Networking;

/// <summary>The outcome of one DNS query: the answer, or why there is none.</summary>
/// <param name="Answer">The decoded answer, holding a record of the type asked for; <see langword="null" /> on failure.</param>
/// <param name="Failure"><see cref="DnsLookupFailure.None" /> when <paramref name="Answer" /> is set; otherwise why it is not.</param>
public sealed record DnsQueryOutcome(DnsAnswer? Answer, DnsLookupFailure Failure)
{
    /// <summary>
    /// Gets a value indicating whether the outcome ends the query: an answer, NOERROR with no data, or
    /// NXDOMAIN. Anything else sends the query on to the next server, as c-ares does.
    /// </summary>
    public bool IsFinal => Failure is DnsLookupFailure.None or DnsLookupFailure.NoData or DnsLookupFailure.NotFound;
}
