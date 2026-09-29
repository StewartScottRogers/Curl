---
id: BL-507
title: Dial a Unix domain socket instead of TCP when --unix-socket or --abstract-unix-socket is given
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-506]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-507 — Dial a Unix domain socket instead of TCP when --unix-socket or --abstract-unix-socket is given

## Goal

With `--unix-socket <path>` the connector opens a `UnixDomainSocketEndPoint` at that path instead of resolving and dialling the URL's host (the URL's host still goes in `Host:`), `--abstract-unix-socket` uses the abstract namespace where the platform has it, and a socket that cannot be opened fails as curl 8.21.0 fails it.

## Context

- Conformance audit 2026-09-28, row 7 (Blocker). Parsing is BL-506.
- Code: `Curl.Networking.UnitLibrary/TcpConnector.cs`, `ITcpDialer.cs`/`TcpDialer.cs` (the thin adapter; ADR-0083), `ConnectDestination.cs`, `PoolingConnector.cs` and `ConnectionPoolKey.cs` (a socket path must be part of the reuse key); wiring in `Curl.Console/CurlTransports.cs`.
- `System.Net.Sockets.UnixDomainSocketEndPoint` is in the BCL and works on Windows 10+, Linux and macOS; an abstract name is a leading NUL and exists only on Linux.
- Measure: a missing path, and `-v` lines for a successful connect (curl prints `Connected to <host> (<path>)`-style lines; take the exact text from the measurement). The loopback server of `Record-CurlExchange.ps1` listens on TCP only; extend it with a `-UnixSocket <path>` listener if needed, as the root `CLAUDE.md` asks, rather than writing another server.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (extended if needed): `--unix-socket <path> -v http://localhost/` against a listening socket and a missing path, on Windows and on Linux or macOS; stdout, stderr and exit code copied into Notes.
- [ ] `Curl.Networking.UnitTests` tests through the `ITcpDialer` seam show the socket endpoint dialled, no DNS lookup, and the measured failure's exit code and message.
- [ ] Two transfers with different socket paths never share a pooled connection; a test shows it.
- [ ] The abstract-namespace answer is pinned per platform (`OSCondition`), as measured.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
