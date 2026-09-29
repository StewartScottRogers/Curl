---
id: BL-884
title: Pass the Unix socket path from TcpConnector to the HTTP handler's left-intact line
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-794]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-884 — Pass the Unix socket path from TcpConnector to the HTTP handler's left-intact line

## Goal

`curl -v --unix-socket <path> http://host:port/x` ends with `* Connection #0 to host <path, ASCII lower-cased>:0 left intact`, because `TcpConnector` and `PoolingConnector` set `ConnectResult.UnixSocketPath`.

## Context

- BL-794 added `ConnectResult.UnixSocketPath` (Abstractions, `Connected(..., unixSocketPath:)`) and made `HttpProtocolHandler` print the socket path, lower-cased, with port 0 when it is set. Nothing sets it yet: BL-609 held `Curl.Networking.UnitLibrary` while BL-794 ran.
- `TcpConnector.ConnectOverUnixSocketAsync` knows the `UnixSocketAddress`; `SecureWhenAskedAsync` builds the `ConnectResult`, so the path has to reach that call (for plain HTTP and for HTTPS over the socket).
- `PoolingConnector.Open` and `Reuse` rebuild the `ConnectResult`; `PoolEntry` must keep the path so a reused connection's left-intact line names the socket too.
- Measured with curl 8.21.0 (Schannel) in BL-794 Notes: the whole path, not cut to 45 characters as the Trying line is.
- Abstract names (`--abstract-unix-socket`): pass the name as given; not measured (no Linux build at hand).

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests` pins that a `TcpConnector` with a `UnixSocket` returns `ConnectResult.UnixSocketPath` equal to the socket's `Path`, and a TCP connect returns `null`.
- [ ] `Curl.Networking.UnitTests` pins that `PoolingConnector` keeps `UnixSocketPath` on both the opened and the reused result.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
