namespace Curl.Networking;

/// <summary>The outcome of <see cref="DnsServerResolver.ResolveServiceAsync" />.</summary>
/// <param name="Records">The SRV records, in answer order; empty on failure.</param>
/// <param name="Failure"><see cref="DnsLookupFailure.None" /> when there are records; otherwise why there are none.</param>
public sealed record DnsServiceLookup(IReadOnlyList<DnsServiceRecord> Records, DnsLookupFailure Failure);
