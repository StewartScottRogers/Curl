---
id: BL-040
title: Implement the UDP IDatagramConnector in Curl.Networking
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-034]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-040 — Implement the UDP IDatagramConnector in Curl.Networking

## Goal

`Curl.Networking.UnitLibrary` has the production `IDatagramConnector` and
`IDatagramChannel` over UDP, resolving through `IDnsResolver` and reporting an
unresolvable host as exit 6 with curl 8.21.0's message.

## Context

The contract is ADR-0005
(`Documentation/Planning/Decisions/ADR-0005-protocol-handlers-acquire-transports-through-connectors.md`)
as implemented by BL-034. TFTP is its only consumer: the first datagram goes to the
server's well-known port (`ServerEndPoint`), and the replies come from a new port that the
handler then sends to (RFC 1350 section 4), which is why `SendAsync` takes a destination
and `ReceiveAsync` reports the sender.

Measured on 2026-09-26 with the local curl 8.21.0 (Release-Date 2026-06-24):
`curl tftp://nonexistent.invalid/x` exits 6 (`CURLE_COULDNT_RESOLVE_HOST`,
<https://curl.se/libcurl/c/libcurl-errors.html>) with
`Could not resolve host: nonexistent.invalid`.

Testability follows the TCP connector task: the opener takes `IDnsResolver` and an
internal socket seam so the fast tests cover resolution and endpoint selection without a
socket, and the one loopback round trip through a real UDP socket is tagged
`[TestCategory("Integration")]`.

## Acceptance criteria

- [x] `UdpDatagramConnector : IDatagramConnector` and a `Socket`-backed
      `IDatagramChannel` exist in `Curl.Networking.UnitLibrary`.
- [x] A test asserts a resolver returning no addresses gives
      `DatagramOpenResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: nonexistent.invalid")`.
- [x] A test asserts a successful open reports `ServerEndPoint` as the first resolved
      address with the requested port.
- [x] The Integration-tagged loopback test sends a datagram to `ServerEndPoint`, receives
      a reply from a different local port, and asserts `DatagramReceived.Length` and
      `RemoteEndPoint` report that other port.
- [x] Disposing the channel closes the socket; cancellation of `ReceiveAsync` surfaces as
      `OperationCanceledException`.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

- Plan: `UdpDatagramConnector(IDnsResolver, TimeProvider)` resolves, then opens a
  `UdpDatagramChannel` for each resolved address in order until one opens; an internal
  constructor takes `Func<IPEndPoint, IDatagramChannel>` so the fast tests cover
  resolution and endpoint selection without a socket. `UdpDatagramChannel` owns one
  unconnected UDP socket bound to the wildcard address of the server's family on an
  ephemeral port, so replies from the server's transfer port are accepted; an internal
  bind seam covers the dispose-on-bind-failure branch.
- Choice (sensible default): when no resolved address can have a socket opened (for
  example an IPv6-only result on a host without IPv6), the result is exit 7 with the
  same message format `TcpConnector` uses, `Failed to connect to <host>:<port> after
  <n> ms: Could not connect to server`, timed by the injected `TimeProvider`. This
  follows the `IDatagramConnector` contract (exit 7 when no channel can be opened); the
  exact curl wording for this rare UDP case was not measured.
- Choice: the dispose and cancellation tests use a local loopback socket that sends
  nothing, so they run with the fast tests; only the send/receive round trip is tagged
  `Integration`, like `TcpDialerTests`.
- Coverage of `UdpDatagramConnector` and `UdpDatagramChannel` measured at 100% line and
  100% branch with the full test run. The project's `CLAUDE.md` now names
  `UdpDatagramChannel` as the only type constructing a UDP `Socket`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. UdpDatagramConnector and the Socket-backed UdpDatagramChannel open TFTP's UDP transport, with exit 6 for an unresolvable host
