---
id: BL-3174
title: Connect to IPv4-mapped IPv6 literals such as [::ffff:127.0.0.1]
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpDialer.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/TcpDialerMappedIPv4Tests.cs, Curl.Console.IntegrationTests/CurlMappedIPv4LiteralIntegrationTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3174 — Connect to IPv4-mapped IPv6 literals such as [::ffff:127.0.0.1]

## Goal

`curl http://[::ffff:127.0.0.1]:18080/echo` connects and returns the response, as upstream does, instead of exiting 7.

## Context

Stewart reported on 2026-10-22: `curl http://[::ffff:127.0.0.1]:18080/echo`; upstream connects and returns 200 and Curl exits 7. Not yet confirmed by the coordinator (it needs a listening server): first reproduce with a loopback listener and record the exact error text, then check curl 8.22.0 with `Record-CurlExchange.ps1`. Likely cause: the IPv6 socket is created with `DualMode` off, so an IPv4-mapped address cannot reach an IPv4-only listener. Fix by making the TCP socket dual-stack (`Socket.DualMode = true`) or by connecting through the mapped IPv4 address (`IPAddress.MapToIPv4`) when the literal is IPv4-mapped; pick the simplest that matches curl on Windows, and note Linux/macOS behaviour (Linux dual-stack sockets depend on `bindv6only`; connecting via the mapped IPv4 address is the platform-neutral choice). The dial code is `Curl.Networking.UnitLibrary/TcpDialer.cs` (the `ITcpDialer` contract is `ITcpDialer.cs`) and the connect loop in `TcpConnector.cs` near line 2398. Local binding (`LocalBindingTcpDialer.cs`) must keep working. `TcpConnector.cs` is also edited by other networking tasks; the board serialises them.

## Acceptance criteria

- [x] `Curl.Networking.UnitTests/TcpDialerMappedIPv4Tests.cs` has a loopback test (a listener on `127.0.0.1`, connecting to `[::ffff:127.0.0.1]` and its port) that connects and exchanges bytes; it is a unit test (a loopback socket opened and closed, per ADR-0421) and skips cleanly with `[OSCondition]` only where IPv6 sockets are unavailable, if any.
- [x] A test through `TcpConnector` with a fake dialler shows the connect target is the IPv4 address (or the dual-mode socket) for a mapped literal, and unchanged for `[::1]` and ordinary IPv4.
- [x] The command-line case `curl http://[::ffff:127.0.0.1]:<port>/` returns exit 0 and the body in a `Curl.Console.UnitTests` test using a loopback fake server.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and the fast tests of `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pass.

## Notes

- Reproduced 2026-10-22: a .NET `InterNetworkV6` socket (default `DualMode = false`) connecting to
  `[::ffff:127.0.0.1]:P` of an IPv4 listener fails with `SocketError.AddressNotAvailable` ("The requested
  address is not valid in its context"), which Curl reported as exit 7; with `DualMode = true` it connects
  from `[::ffff:127.0.0.1]`.
- curl 8.22.0 (Windows, `Record-CurlExchange.ps1 -CurlArgs '-v','http://[::ffff:127.0.0.1]:18174/echo'`):
  exit 0, body `hello`, `Established connection to [::ffff:127.0.0.1] (::ffff:127.0.0.1 port 18174) from
  ::ffff:127.0.0.1 port 51971`, `Host: [::ffff:127.0.0.1]:18174`. Its local end is the mapped address, so
  curl uses a dual-stack IPv6 socket.
- Decision (ADR-0778): `TcpDialer.ApplySocketOptions` sets `DualMode = true` on every IPv6 socket;
  `TcpConnector` is unchanged and dials the literal as written, so the reported end points stay curl's.
  Linux and macOS default to dual-stack too, and setting it explicitly also covers `bindv6only=1` hosts.
- Criterion 1: the loopback dialer test connects and closes without sending a byte, since ADR-0421 makes a
  loopback socket that exchanges bytes an Integration test; no `[OSCondition]` is needed (IPv6 sockets open
  on all three CI platforms). The byte exchange is covered by the command-line test.
- Criterion 2: the connector test shows the dial target unchanged for the mapped literal, `::1` and
  `127.0.0.1` (the dual-mode socket path).
- Criterion 3: the command-line test sends and receives bytes over a loopback socket, so by ADR-0421 it is
  `Curl.Console.IntegrationTests/CurlMappedIPv4LiteralIntegrationTests.cs`, not `Curl.Console.UnitTests`;
  added to `touches` (no task in Doing names it). It passes.
- `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members.
- CI on Linux and macOS: the change is platform-neutral (no paths, error text or OS-specific values) and
  the fast tests are green here.

- Lane 7 resumed lane 1's commit 793cd681c (its rebase conflicted only in the ADR README row) and renumbered its ADR to 0776, as 0775 was taken by BL-3200.
- Lane 7 re-ran the checks: Networking build -warnaserror clean; fast tests Curl.Networking.UnitTests 3403 passed, Curl.Console.UnitTests 2843 passed; the mapped-literal integration test passed; Measure-CodeQuality -Library Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: d4457215 81057b64 52b179ac d7d8ce4c.
- 2026-10-22: Doing -> Backlog. Lane 1 could not integrate: rebase conflict the resolver could not settle. The work is on branch factory/BL-3174-lane-1-20261022-192710; start with git cherry-pick --no-commit factory/BL-3174-lane-1-20261022-192710 and fix it.
- 2026-10-22: Backlog -> Doing. Criteria at claim: d4457215 81057b64 52b179ac d7d8ce4c.
