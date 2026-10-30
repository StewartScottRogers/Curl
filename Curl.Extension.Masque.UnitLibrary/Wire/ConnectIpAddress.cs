using System.Net;

namespace Curl.Extension.Masque.Wire;

/// <summary>
/// One entry of an <c>ADDRESS_ASSIGN</c> or <c>ADDRESS_REQUEST</c> capsule (RFC 9484 sections 4.7.1 and 4.7.2).
/// </summary>
/// <param name="RequestId">The request it answers or makes; 0 in an assignment nobody asked for.</param>
/// <param name="Address">The IPv4 or IPv6 address.</param>
/// <param name="PrefixLength">The prefix length, at most 32 for IPv4 and 128 for IPv6.</param>
internal sealed record ConnectIpAddress(ulong RequestId, IPAddress Address, byte PrefixLength);
