using System.Net;

namespace Curl.Networking;

/// <summary>
/// One key of curl's DNS cache (<see cref="DnsCache" />): the host as it was cached (the name
/// looked up, or a <c>--resolve</c> entry's host as written), the port and the addresses.
/// </summary>
/// <param name="Name">The host as it was cached.</param>
/// <param name="Port">The port.</param>
/// <param name="Addresses">The addresses.</param>
internal sealed record DnsCacheEntry(string Name, int Port, IReadOnlyList<IPAddress> Addresses);
