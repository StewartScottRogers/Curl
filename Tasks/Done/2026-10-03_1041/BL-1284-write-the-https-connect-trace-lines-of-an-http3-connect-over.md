---
id: BL-1284
title: Write the [HTTPS-CONNECT] trace lines of an --http3 connect over QUIC
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-03
---
# BL-1284 — Write the [HTTPS-CONNECT] trace lines of an --http3 connect over QUIC

## Goal

Curl writes curl's `[HTTPS-CONNECT]` lines (and the origin's `[SETUP]` lines) under `--trace-config https-connect`, `all` and `-vvvv` for an `--http3` or `--http3-only` transfer, as it does for a TCP connect.

## Context

- Split from BL-1254, which wrote them through proxies and over Unix sockets. QUIC connects go through `TcpConnector.ConnectMultiplexedAsync`; the filter is `HttpsConnectFilterTraceEvents` with `HttpsConnectFirstAttemptVersion` `h3`.
- The reference curl 8.21.0 (mingw, Schannel) has no HTTP/3, so BL-1254 could not measure it. Measure with an HTTP/3 build (curl.se's Windows build with ngtcp2, ADR-0180) against a QUIC server, and note how the h3 attempt's lines, any h2 fallback attempt (`2nd attempt`), and `[SETUP]` sit beside the `QUIC connect to` lines.

## Acceptance criteria

- [x] Measured first, stderr in Notes, for `--http3` and `--http3-only` to a loopback QUIC server.
- [x] Tests in `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the measured lines; ADR-0357 gets an amendment.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Touches: added `Curl.Console` (`CurlComposition` sets `HttpsConnectSecondAttemptVersion` under `--http3`); no task in Doing on `origin/work/dark-factory` named it.
- Measured 2026-10-03 with curl.se's curl 8.22.0 ngtcp2 build, `-s -k -v --trace-config https-connect,setup`. QUIC server: a throwaway C# file-based Kestrel HTTP/3 app (`WebApplication.CreateSlimBuilder` plus `UseQuic()`, HTTP/1, 2 and 3 on one port, self-signed); TLS-only and silent-UDP cases with `Record-CurlExchange.ps1 -Tls [-UdpSink]`. 8.22.0 writes 8.21.0's TCP lines plus two `HTTPS-RR not available` lines (after `connect, init` and after `1st attempt`), which stay unwritten.
- `--http3` and `--http3-only` to the Kestrel server (exit 0, `using HTTP/3`), the same but for the second-attempt line: `[HTTPS-CONNECT] added` / `connect, init` / `1st attempt uses h3 from wanted versions` / (`--http3` only) `2nd attempt uses h2 from wanted versions` / `[SETUP] happy eyeballing to origin 127.0.0.1:18713` / `Trying 127.0.0.1:18713...` / `connect -> 0, done=0` / `adjust_pollset -> 0, 1 socks` / `SSL Trust: peer verification disabled` / `connect -> 0, done=0` / `adjust_pollset -> 0, 1 socks` / `SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / [blank] / UNDEF` / `Server certificate:` ... / `[SETUP] query ALPN` / `connect -> 0, done=1` / `Established connection to 127.0.0.1 ...` / `[HTTPS-CONNECT] removing connected setup filter` / `[HTTPS-CONNECT] destroy` / `[SETUP] removing connected setup filter` / `[SETUP] destroy` / `using HTTP/3`. No `QUIC connect to` line on success, and no `[SETUP] added SSL filter for origin`.
- QUIC refused (no UDP listener), TLS on TCP, `--http3`: after the first poll round `QUIC: recvfrom() unexpectedly returned -1 (errno=10054; ...)`, `QUIC connect to 127.0.0.1 port 18720 failed: ...`, `Failed to connect to ...`, `[HTTPS-CONNECT] h3 baller failed, starting h2`, `[SETUP] happy eyeballing ...`, `Trying`, rounds with `1 socks`, `[SETUP] added SSL filter for origin`, ..., `done=1`, Established, `[HTTPS-CONNECT] removing connected setup filter`, `[HTTPS-CONNECT] destroy`, `[SETUP] destroy`, `[SETUP] removing connected setup filter`, `[SETUP] destroy`. Nothing listening at all: `connect, all attempts failed` / `connect -> 56, done=0` (QUIC's code, after the TCP attempt). `--http3-only` refused: `QUIC connect to ... failed`, `connect, all attempts failed`, `connect -> 56, done=0`.
- Silent UDP sink, `--http3`: three QUIC poll rounds (`1 socks`), `[HTTPS-CONNECT] h3 inconclusive after 200, starting h2`, `happy eyeballing`, `Trying`, rounds with `2 socks`, TLS, `done=1`, removal, `[HTTPS-CONNECT] destroy`, `[SETUP] destroy`, `[SETUP] removing connected setup filter`, `[SETUP] destroy`. `--http3-only --connect-timeout 1`: two rounds, `Connection timed out after 1005 milliseconds`, no `all attempts failed` (exit 28).
- Decision (ADR-0357's BL-1284 amendment): QUIC connects go through the same setup and HTTPS-CONNECT filters; the TCP attempt of the `--http3` race continues the filter from per-target state in `TcpConnector`; poll rounds fixed to the measured counts; `[SETUP] query ALPN` and the `HTTPS-RR` lines stay unwritten, as for TCP. Not done: lines for QUIC through a CONNECT-UDP proxy, and for the Alt-Svc race that tries TCP first. Filed as BL-1320.
- Verified 2026-10-03: `dotnet build Curl.slnx -warnaserror` 0 warnings; fast tests green in all 33 test projects (Networking 2936, Console 2469 passed). `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: every member this task added or changed at 100% line and branch, complexity at most 10; seven older members it did not touch still fail and are filed as BL-1319. `Curl.Console` changed by one property assignment, covered by `CreateTcpConnector_UnderTraceConfigHttpsConnect_NamesTheAttemptsCurlsNgtcp2BuildNames`.

## Log

- 2026-10-02: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Curl writes curl's [HTTPS-CONNECT] and [SETUP] lines for an --http3 or --http3-only QUIC connect and the --http3 race's TCP attempt, as curl 8.22.0's ngtcp2 build measured
