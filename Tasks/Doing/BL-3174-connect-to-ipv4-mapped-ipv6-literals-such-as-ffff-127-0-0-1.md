---
id: BL-3174
title: Connect to IPv4-mapped IPv6 literals such as [::ffff:127.0.0.1]
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Networking.UnitLibrary/TcpDialer.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/TcpDialerMappedIPv4Tests.cs]
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

- [ ] `Curl.Networking.UnitTests/TcpDialerMappedIPv4Tests.cs` has a loopback test (a listener on `127.0.0.1`, connecting to `[::ffff:127.0.0.1]` and its port) that connects and exchanges bytes; it is a unit test (a loopback socket opened and closed, per ADR-0421) and skips cleanly with `[OSCondition]` only where IPv6 sockets are unavailable, if any.
- [ ] A test through `TcpConnector` with a fake dialler shows the connect target is the IPv4 address (or the dual-mode socket) for a mapped literal, and unchanged for `[::1]` and ordinary IPv4.
- [ ] The command-line case `curl http://[::ffff:127.0.0.1]:<port>/` returns exit 0 and the body in a `Curl.Console.UnitTests` test using a loopback fake server.
- [ ] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean and the fast tests of `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pass.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: d4457215 81057b64 52b179ac d7d8ce4c.
- 2026-10-22: Doing -> Backlog. Lane 1 could not integrate: rebase conflict the resolver could not settle. The work is on branch factory/BL-3174-lane-1-20261022-192710; start with git cherry-pick --no-commit factory/BL-3174-lane-1-20261022-192710 and fix it.
- 2026-10-22: Backlog -> Doing. Criteria at claim: d4457215 81057b64 52b179ac d7d8ce4c.
