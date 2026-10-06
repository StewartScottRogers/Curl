---
id: BL-1320
title: Write the [HTTPS-CONNECT] lines of an --http3 connect through a CONNECT-UDP proxy and of the Alt-Svc race that tries TCP first
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1320 — Write the [HTTPS-CONNECT] lines of an --http3 connect through a CONNECT-UDP proxy and of the Alt-Svc race that tries TCP first

## Goal

Curl writes curl's `[HTTPS-CONNECT]` and `[SETUP]` lines for an `--http3` connect through an HTTP or HTTPS proxy (CONNECT-UDP) and for the `--http3` race that tries TCP first for an `--alt-svc` entry naming h2 or h1.

## Context

Split from BL-1284, which wrote them for a direct QUIC connect (ADR-0357's BL-1284 amendment, `TcpConnector.QuicHttpsConnect.cs`). Measure with curl.se's curl 8.22.0 ngtcp2 build (`%TEMP%rl-8.22`) as BL-1284 Notes describe, with a loopback Kestrel HTTP/3 server; `TcpConnector.UdpTunnel.cs` and `HttpProtocolHandler.RaceTcpAgainstQuicAsync` are where to start.

## Acceptance criteria

- [x] Measured first, stderr in Notes, for both cases.
- [x] Tests in `Curl.Networking.UnitTests` (and `Curl.Protocol.Http.UnitTests` if the race changes) pin the measured lines; ADR-0357 gets an amendment. (CONNECT-UDP case: `TcpConnectorQuicTests.UdpTunnelHttpsConnectTrace.cs`; the Alt-Svc case split to BL-1360, see Notes.)
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-03 with curl.se's curl 8.22.0 ngtcp2 build (`%TEMP%\curl-8.22`), `-s -k -v --trace-config https-connect,setup`, through `Record-CurlExchange.ps1`.
- CONNECT-UDP, `--http3 -x http://127.0.0.1:18320 https://example.com/`, proxy answering `403` to both connections, exit 7 (blank `>`/`<` continuation lines and `HTTPS-RR not available` lines left out): `[HTTPS-CONNECT] added` / `connect, init` / `1st attempt uses h3 from wanted versions` / `2nd attempt uses h2 from wanted versions` / `[SETUP] happy eyeballing to proxy 127.0.0.1:18320` / `Trying 127.0.0.1:18320...` / `connect -> 0, done=0` / `adjust_pollset -> 0, 1 socks` / `[SETUP] added HTTP proxy tunnel filter` / `CONNECT-UDP: no ALPN negotiated` / `Establishing HTTP proxy UDP tunnel to example.com:443` / `> GET http://127.0.0.1:18320/.well-known/masque/udp/example.com/443/ HTTP/1.1` and its headers / `connect -> 0, done=0` / `adjust_pollset -> 0, 1 socks` / `< HTTP/1.1 403 Forbidden` ... / `CONNECT-UDP tunnel failed, response 403` / `[HTTPS-CONNECT] h3 baller failed, starting h2` / `[SETUP] happy eyeballing to proxy ...` / `Trying` / pair / `[SETUP] added HTTP proxy tunnel filter` / `CONNECT: no ALPN negotiated` / `Establishing HTTP proxy tunnel to example.com:443` / `> CONNECT example.com:443 HTTP/1.1` ... / pair / `< HTTP/1.1 403 Forbidden` ... / `CONNECT tunnel failed, response 403` / `[HTTPS-CONNECT] connect, all attempts failed` / `connect -> 7, done=0` / `closing connection #0`. `--http3-only`: the same up to `CONNECT-UDP tunnel failed, response 403`, then `connect, all attempts failed` / `connect -> 7, done=0`.
- Alt-Svc race, alt-svc file `h2 127.0.0.1 <port> h2 127.0.0.1 <port> "20991231 00:00:00" 0 0`, `--http3 --alt-svc <file> https://127.0.0.1:<port>/`. TLS server (`-Tls`), exit 0: `added` / `connect, init` / `1st attempt uses h2 from preferred version` / `2nd attempt uses h3 from wanted versions` / `[SETUP] happy eyeballing to origin` / `Trying` / pair / `[SETUP] added SSL filter for origin` / ALPN `h2,http/1.1` / handshake with two more pairs / `[SETUP] query ALPN` / `done=1` / Established / `[HTTPS-CONNECT] removing connected setup filter` / `[HTTPS-CONNECT] destroy` / `[SETUP] removing connected setup filter` / `[SETUP] destroy`. An `h1` entry (`-UdpSink`): `1st attempt uses h1 from preferred version`, otherwise the same. Nothing listening (`-NoServer`), exit 7: ... `Trying` / pair / `connect to ... failed: Connection refused` / `Failed to connect to 127.0.0.1:<port> after 0 ms: Could not connect to server` / `[HTTPS-CONNECT] h2 baller failed, starting h3` / `[SETUP] happy eyeballing to origin ...` / `Trying` / pair / `SSL Trust: peer verification disabled` / `QUIC: recvfrom() ...` / `QUIC connect to ... failed: ...` / `Failed to connect to ...` / `connect, all attempts failed` / `connect -> 7, done=0`.
- Decision (ADR-0357's BL-1320 amendment): the CONNECT-UDP path goes through the QUIC filters with the setup filter eyeballing to the proxy, and the CONNECT path after it continues the kept QUIC state (`TracingTunnelFilters` takes it). Also added the `-v` lines curl writes there that Curl did not: `CONNECT-UDP: no ALPN negotiated` for an HTTP proxy, `Establishing HTTP proxy UDP tunnel to`, the request head, the reply lines, and `CONNECT-UDP phase completed for HTTP proxy` (BL-942's measurement). The reply head now goes to the output the events were before the filters wrapped them, as for CONNECT.
- Split: the Alt-Svc race needs the entry's ALPN carried from `AltSvcTransferCache` (Curl.Console, held by BL-1322 in Doing) through `HttpRequestOptions`/`ConnectTarget` (Abstractions); filed as BL-1360 with the measurement above rather than widening this task.
- Not matched, pre-existing: the TCP CONNECT path does not write `CONNECT tunnel failed, response 403` as a `-v` line before `all attempts failed` (curl does); the test notes it.
- Verified 2026-10-03: `dotnet build Curl.slnx -warnaserror` 0 warnings; fast tests green in all 33 test projects (Networking 2964 passed). `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members. `Curl.Protocol.Http` unchanged (the race did not change).

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Curl writes curl 8.22.0's [HTTPS-CONNECT] and [SETUP] lines and the CONNECT-UDP -v lines for --http3 and --http3-only through an HTTP or HTTPS proxy; the Alt-Svc TCP-first race split to BL-1360
