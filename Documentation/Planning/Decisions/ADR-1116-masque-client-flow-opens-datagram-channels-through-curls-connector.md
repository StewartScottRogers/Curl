# ADR-1116: The MASQUE client flow opens datagram channels through curl's connector

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4317
- Decided by Claude under Stewart's delegation

## Context

BL-4317 is the second part of BL-4144's split: on ADR-1115's wire format, an
`IDatagramConnector` that opens a CONNECT-UDP (RFC 9298) or CONNECT-IP (RFC 9484) tunnel to a
proxy through curl's `IConnector` and carries datagrams in it. Upstream curl has no MASQUE
extension, so the behaviour follows the RFCs and, where they leave a choice, `Curl.Networking`'s
`CapsuleDatagramChannel` (ADR-0726), curl's own `-x` CONNECT-UDP tunnel, which the extension may
not reference.

## Decision

- `MasqueDatagramConnector` (in `Tunnel/`) expands the URI template, connects to its host and port
  through the `IConnector` it is given, with TLS for an `https` template, writes ADR-1115's
  upgrade request and checks the reply with `MasqueUpgrade.CheckReply`.
- Failures map to curl's exit codes: a proxy the connector cannot reach keeps the connector's code
  and message; a refused upgrade (with its status), a proxy that closes the stream before the
  tunnel is open, a template that does not expand, a malformed capsule and an `IOException` or
  `SocketException` while opening are exit 7 (`CouldntConnect`), the message the cause's own. Only
  `OperationCanceledException` escapes, as `IDatagramConnector` requires. The connection is
  disposed on every failure after it opened.
- A CONNECT-UDP channel sends each datagram as one `DATAGRAM` capsule with Context ID 0 (RFC 9298
  section 5) to the one target, whatever destination a send names, and reports every received
  datagram as from the target (an `IPEndPoint` for an address literal, else a `DnsEndPoint`).
- A CONNECT-IP tunnel's template is expanded with the target's address and `ipproto` 17 (UDP). Its
  packets carry the target's address, so the target must be an IP address literal; a host name is
  exit 6 (`CouldntResolveHost`) before anything is connected, since the extension owns no resolver.
- CONNECT-IP sends one `ADDRESS_REQUEST` (request ID 1) for the unspecified address of the target's
  family with a full-length prefix, which RFC 9484 section 4.7.2 reads as "any address", and uses
  the first address of that family in the first `ADDRESS_ASSIGN` holding one; capsules before it
  are dropped. The channel's UDP source port is picked at random from the IANA ephemeral range
  (49152 to 65535), as an operating system picks one for an unbound socket.
- Received capsules of unknown type are dropped (RFC 9297 section 3.2), as are datagrams with a
  Context ID other than 0 and, on CONNECT-IP, packets that are not UDP or not from the target's
  address to the channel's own address and port. A later `ADDRESS_ASSIGN` is dropped too: the
  channel keeps the address it opened with.
- A tunnel the proxy closes fails a receive with `SocketException` `ConnectionReset`, as a reset
  UDP socket does and as `CapsuleDatagramChannel` reports it. A datagram longer than the receive
  buffer is cut to fit.
- `MasqueTunnelStream` buffers at most 1 MiB while waiting for the end of a reply head or capsule,
  so a proxy cannot make Curl buffer without limit; past it the open fails with exit 7.

## Consequences

The option that turns the connector on (BL-4144) only builds a `MasqueDatagramConnector` from its
template and returns it from its connection hook's `DatagramConnectorOver`. A CONNECT-IP tunnel to
a host name needs a resolver the extension does not have; that is left until a task asks for it.
