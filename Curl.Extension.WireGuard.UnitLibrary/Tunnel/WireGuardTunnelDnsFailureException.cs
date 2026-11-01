using Curl.Protocol.Abstractions;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>
/// Carries the failure to reach the tunnel's DNS server out of <see cref="WireGuardTunnelDnsResolver" />, through curl's
/// connector, to <see cref="WireGuardConnector" />'s QUIC methods, which turn it into the connect failure (exit 7) the
/// TCP path gives, not the unresolved name (exit 6) an empty answer would be (ADR-1172, BL-4424).
/// </summary>
/// <param name="failure">The connect failure the exchange with the DNS server ended in.</param>
internal sealed class WireGuardTunnelDnsFailureException(ConnectResult failure) : Exception(failure.ErrorMessage)
{
    /// <summary>Gets the connect failure the exchange with the DNS server ended in.</summary>
    public ConnectResult Failure { get; } = failure;
}
