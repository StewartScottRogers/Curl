---
id: BL-2018
title: Detect a kept HTTP connection the server closed before sending a retry on it, as curl's Connection N seems to be dead
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2018 — Detect a kept HTTP connection the server closed before sending a retry on it, as curl's Connection N seems to be dead

## Goal

When a response Curl answers with another request on the same connection (a 401 or 407 challenge, a 417 resend) leaves a connection the server has already closed, Curl writes curl 8.21.0's `Connection N seems to be dead` and `shutting down connection #N`, and sends the request on a fresh connection without writing a byte to the dead one.

## Context

Found by BL-2000 (GF-0004 re-close), measured on 2026-10-10 with curl 8.21.0 (Schannel, Windows) against a loopback server that answers `GET /64` with a 401 Digest challenge (`Content-Length: 26`), then shuts down its send side and keeps reading:

- Real curl: `Connection #0 ... left intact`, `Issue another request to this URL: '...'`, `Connection 0 seems to be dead`, `shutting down connection #0`, `Hostname 127.0.0.1 was found in DNS cache`, `Trying ...`, then the authenticated request goes out on connection #1. The server reads **nothing** more on connection #0.
- Curl: `Reusing existing http: connection with host 127.0.0.1`, writes the 219-byte authenticated request to connection #0, then reads EOF and retries with `Connection died, retrying a fresh connect (retry count: 1)`. Exit 0, but the request bytes and `-v` lines differ from curl's.

curl checks a kept connection before reusing it (`Curl_conn_is_alive`: the socket is readable and a peek reads 0 bytes). Curl has no such probe: `IConnection` (Curl.Protocol.Abstractions.UnitLibrary) has no liveness member, `PoolingConnector.TakeIdleAsync` only knows a connection is dead when an earlier read returned 0 (`PoolEntry.HasReadPeerClose`, ADR-0112), and `HttpProtocolHandler.ExchangeWithRetriesAsync` sends a retry on the same connection while `KeepsAlive` holds, without asking.

Where to start: add a liveness question to `IConnection` (default "alive"), answer it in `StreamConnection` over a socket (`Socket.Poll(0, SelectRead)` with `Available == 0`) and forward it through `PooledConnection`, `SslStreamConnection` and the hand-built TLS connection; ask it in `ExchangeWithRetriesAsync` before `ReportRetryOnSameConnection` and in `PoolingConnector.TakeIdleAsync`. Keep the socket call inside the `[ExcludeFromCodeCoverage]` transport seam (ADR-0083). Record the decision in an ADR.

This may be what GF-0004's gap harness meets (BL-2000, BL-2019); with a plain FIN or RST close Curl already matches curl.

## Acceptance criteria

- [ ] With a server that answers a Digest 401 and half-closes, Curl's `-v` lines and request bytes match curl 8.21.0's above (measured with a loopback server, recorded under Notes).
- [ ] A unit test in `Curl.Protocol.Http.UnitTests` pins the retry going to a fresh connection with `Connection N seems to be dead` when the connection reports itself closed, and none of its bytes written to the dead one.
- [ ] A unit test in `Curl.Networking.UnitTests` pins the pool reporting a closed idle connection dead through the new probe.
- [ ] Every changed `*.UnitLibrary` keeps 100% line and branch coverage (`Measure-CodeQuality.ps1 -Library ...`).
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green.
- [ ] No option changes, so `--ai-help` needs nothing; say so under Notes.

## Notes

## Log

- 2026-10-10: Created.
