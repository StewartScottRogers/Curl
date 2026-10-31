namespace Curl.Extension.WireGuard.Configuration;

/// <summary>A peer's <c>Endpoint</c>: a host name or IP address and a UDP port.</summary>
/// <param name="Host">The host name or IP address, without the brackets an IPv6 address is written in.</param>
/// <param name="Port">The UDP port, 1 to 65535.</param>
public sealed record WireGuardEndpoint(string Host, int Port);
