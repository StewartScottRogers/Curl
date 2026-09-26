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
completed:
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

- [ ] `UdpDatagramConnector : IDatagramConnector` and a `Socket`-backed
      `IDatagramChannel` exist in `Curl.Networking.UnitLibrary`.
- [ ] A test asserts a resolver returning no addresses gives
      `DatagramOpenResult.Failed(CurlExitCode.CouldntResolveHost, "Could not resolve host: nonexistent.invalid")`.
- [ ] A test asserts a successful open reports `ServerEndPoint` as the first resolved
      address with the requested port.
- [ ] The Integration-tagged loopback test sends a datagram to `ServerEndPoint`, receives
      a reply from a different local port, and asserts `DatagramReceived.Length` and
      `RemoteEndPoint` report that other port.
- [ ] Disposing the channel closes the socket; cancellation of `ReceiveAsync` surfaces as
      `OperationCanceledException`.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and
      `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` is
      green.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
