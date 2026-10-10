---
id: BL-2018
title: Detect a kept HTTP connection the server closed before sending a retry on it, as curl's Connection N seems to be dead
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Record-CurlExchange.ps1, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
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

- [x] With a server that answers a Digest 401 and half-closes, Curl's `-v` lines and request bytes match curl 8.21.0's above (measured with a loopback server, recorded under Notes).
- [x] A unit test in `Curl.Protocol.Http.UnitTests` pins the retry going to a fresh connection with `Connection N seems to be dead` when the connection reports itself closed, and none of its bytes written to the dead one.
- [x] A unit test in `Curl.Networking.UnitTests` pins the pool reporting a closed idle connection dead through the new probe.
- [x] Every changed `*.UnitLibrary` keeps 100% line and branch coverage (`Measure-CodeQuality.ps1 -Library ...`).
- [x] `dotnet build -warnaserror` is clean and the fast tests are green.
- [x] No option changes, so `--ai-help` needs nothing; say so under Notes.

## Notes

- Design (ADR-0467): `IConnection.HasPeerClosed` (default `false`), answered by `StreamConnection` over a `NetworkStream` (`Poll(0, SelectRead)` and `Available == 0`, in an `[ExcludeFromCodeCoverage]` adapter, ADR-0083), forwarded by `SslStreamConnection`, `HandBuiltTlsConnection`, `TcpIoTraceConnection` and `PooledConnection` (which also answers `true` after a read found the close). `HttpProtocolHandler.ExchangeWithRetriesAsync` sends a retry on the same connection only while it is `false`; otherwise the connection is left intact and the retry is reissued, so `PoolingConnector.TakeIdleAsync`, which now asks the probe too, writes `Connection N seems to be dead` and `shutting down connection #N` and connects afresh. That is curl's order exactly, so the handler writes no dead lines itself: the HTTP unit test pins the fresh connect, no `Reusing`, no `Connection died`, and one request on the closed connection; the dead lines are pinned by `PoolingConnectorTests.ConnectAsync_WhenAnIdleConnectionReportsItsPeerClosed_ReportsItDeadAndOpensANewOne`.
- Touches widened (no Doing task names them, checked on origin/work/dark-factory): `Curl.Protocol.Abstractions.UnitTests` for the default member's test, and `Record-CurlExchange.ps1`, which gained `-HalfCloseAfterResponse` (FIN after each response, then keep reading) to measure this.
- Measured 2026-10-10, `Record-CurlExchange.ps1 -Connections 2 -HalfCloseAfterResponse -HoldOpenMilliseconds 1500`, Digest 401 with `Content-Length: 26` then `200 ok`, `--digest -u u:p -v -s http://127.0.0.1:18418/64`:
  - curl 8.21.0 (Schannel): 2 of 3 runs `left intact`, `Issue another request`, `Connection 0 seems to be dead`, `shutting down connection #0`, `Hostname 127.0.0.1 was found in DNS cache`, `Trying`, the retry on #1; one run lost the race to the FIN and reused #0 (`Connection died, retrying a fresh connect`). curl's check is racy too.
  - Curl after this change, 2 of 2 runs: the same `-v` lines, line for line, exit 0, nothing more written to #0.
  - Request bytes: identical except the Digest `Authorization` separators - curl's Schannel build writes `username="u",realm="r",...`, Curl writes `username="u", realm="r", ...` (274 vs 278 bytes). Pre-existing and unrelated to the connection; filed as BL-2022.
- Coverage: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary,Curl.Networking.UnitLibrary,Curl.Protocol.Http.UnitLibrary` (14:11): line 100% in all three, branch 100% except `HttpProtocolHandler.RetryOfAsync` (91.67%, untouched here); the other failing rows are IL-complexity rows on members this task did not change, except `ExchangeWithRetriesAsync` (12), whose loop condition moved into `RetryOnSameConnection` afterwards. Every new branch has a test: the default member (`IConnectionTests`), `StreamConnection` without a socket (the socket call is excluded), each forwarder (TLS provider tests, `TcpIoTraceConnectionTests`), `PooledConnection.HasPeerClosed` both halves, `TakeIdleAsync`'s new condition, and the handler's loop both ways.
- `--ai-help`: no option changed, so nothing to update.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. A kept HTTP connection the server closed is reported dead (Connection N seems to be dead) and the retry goes to a fresh connection, as curl 8.21.0 does
