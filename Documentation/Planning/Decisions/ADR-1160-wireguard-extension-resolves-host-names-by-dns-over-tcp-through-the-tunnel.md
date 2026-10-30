# ADR-1160: The WireGuard extension resolves host names by DNS over TCP through the tunnel

- Status: Accepted
- Date: 2026-10-30
- Task: BL-4354
- Decided by Claude under Stewart's delegation

## Context

ADR-1159 tunnels each connection of `--wireguard <conf>` to an IP address only: `WireGuardConnector` failed a host
name with exit 6 rather than ask the system resolver, whose query would leave outside the tunnel. wg-quick(8) points
the system at the `[Interface]` section's `DNS` servers while the interface is up; a user-space tunnel cannot, so it
must ask them itself. Its IP stack (ADR-1121) carries TCP alone.

## Decision

- **Transport.** A host name is resolved by one DNS over TCP query (RFC 1035 section 4.2.2, RFC 7766): a two-byte
  length prefix and a recursive query (RD set) with a random ID from the connector's `RandomNumberGenerator`. The
  query runs over its own tunnel - a handshake and TCP connection to port 53 of the server, closed once the answer
  is read - so the transfer's connection afterwards is the same single-connection tunnel ADR-1159 opens for an
  address. UDP DNS waits for the IP stack's UDP (BL-4355).
- **Server.** The first `DNS` entry that is an IP address; the other entries are wg-quick's search domains and are
  not used. Cryptokey routing picks the peer for it like any destination.
- **Record type.** A when the interface has an IPv4 `Address`, else AAAA (RFC 3596); the first address of that type
  in the answer section wins, and any other record (the CNAME chain a recursive server returns) is skipped. An
  international name is sent as its A-label (`IdnMapping`, RFC 5891).
- **Failures.** No IP address among the `DNS` entries, an invalid name, a DNS server no peer routes, a server that
  cannot be reached, resets or closes before the whole answer, an answer with another ID, a non-zero RCODE or no
  address of the type asked for: exit 6, `Could not resolve host: <name>`, as curl words a failed lookup. Nothing
  is ever sent to the system resolver or outside the tunnel.

## Consequences

- `curl --wireguard wg0.conf http://name/` works when the configuration has a `DNS` line, at the cost of a second
  handshake per connection.
- `WireGuardDnsMessage` (in `Tunnel/`) is tested against RFC 1035 and the connector against `TunnelTestPeer`, with
  no network.
