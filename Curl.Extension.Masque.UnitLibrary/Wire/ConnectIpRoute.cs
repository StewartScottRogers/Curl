using System.Net;

namespace Curl.Extension.Masque.Wire;

/// <summary>
/// One IP address range of a <c>ROUTE_ADVERTISEMENT</c> capsule (RFC 9484 section 4.7.3).
/// </summary>
/// <param name="Start">The first address of the range.</param>
/// <param name="End">The last address of the range, of the same family as <paramref name="Start" />.</param>
/// <param name="IpProtocol">The IP protocol the range carries, 0 for any.</param>
internal sealed record ConnectIpRoute(IPAddress Start, IPAddress End, byte IpProtocol);
