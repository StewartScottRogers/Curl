using System.Net;

namespace Curl.Networking;

/// <summary>The outcome of <see cref="IDnsResolverWithFailureReason.ResolveWithFailureReasonAsync" />.</summary>
/// <param name="Addresses">The addresses, in the order to try them; empty on failure.</param>
/// <param name="Failure"><see cref="DnsLookupFailure.None" /> when there are addresses; otherwise why there are none.</param>
public sealed record DnsResolution(IReadOnlyList<IPAddress> Addresses, DnsLookupFailure Failure);
