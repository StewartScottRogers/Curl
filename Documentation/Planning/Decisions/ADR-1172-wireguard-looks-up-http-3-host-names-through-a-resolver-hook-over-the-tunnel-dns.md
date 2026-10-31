# ADR-1172: `--wireguard` looks up HTTP/3 host names through a resolver hook over the tunnel's DNS

- Status: Accepted
- Date: 2026-10-31
- Task: BL-4392
- Decided by Claude under Stewart's delegation.

## Context

Under `--wireguard` the TCP path resolves a host name by DNS over TCP through the tunnel (ADR-1161). The HTTP/3
path does not: `TcpConnector.ConnectMultiplexedAsync` resolves the host with curl's resolver before it dials QUIC,
and `DatagramConnectorUdpChannelOpener` hands the tunnel's datagram connector an address, so the query left outside
the tunnel and a name only the tunnel's DNS knows failed with exit 6.

## Decision

The `--wireguard` extension also registers a resolver hook (`WireGuardExtension.ActivateResolver`, one line in
`ExtensionResolverHookCatalogue`). Its resolver ignores curl's and asks `WireGuardConnector.ResolveThroughTunnelAsync`;
an address literal answers itself. The seam already feeds curl's connector (ADR-1012), so the host name is never
passed down to the opener, and TCP and IP-literal QUIC are unchanged.

## Consequences

- A tunnel DNS failure or no `DNS` line gives no address, so exit 6 over HTTP/3, where TCP says exit 7 for an
  unreachable DNS server.
- `--wireguard` now claims the resolver, so it conflicts with `--doh-url` and the other resolver extensions, as
  they conflict with each other.
