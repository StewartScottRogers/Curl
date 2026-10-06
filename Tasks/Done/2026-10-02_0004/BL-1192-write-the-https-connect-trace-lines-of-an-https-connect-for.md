---
id: BL-1192
title: Write the [HTTPS-CONNECT] trace lines of an https:// connect for --trace-config
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1192 — Write the [HTTPS-CONNECT] trace lines of an https:// connect for --trace-config

## Goal

Curl writes curl 8.21.0's `[HTTPS-CONNECT]` lines (the ALPN connect filter of an https:// transfer) under `--trace-config https-connect`, `all` and `-vvvv`.

## Context

- Split from BL-1160 (ADR-0357's BL-1160 amendment), which delivered the [HAPROXY] lines and pinned [SETUP] through a plain HTTP proxy. The TLS route is `TcpConnector.SecureWhenAskedAsync` and `AuthenticateTargetAsync`; `Record-CurlExchange.ps1 -Tls` plays the server. Check how the lines sit beside `[SETUP]` and a `--haproxy-protocol` `[HAPROXY]` line over TLS (BL-1160 put the [HAPROXY] removal after `Established connection` there unmeasured).
- Measure first with `Record-CurlExchange.ps1`; extend it to play the proxy if it cannot (it serves one HTTP exchange, so a plain `-x` proxy works already). Each line's place relative to `[SETUP]`, `[DNS]`, `Established connection` and the CONNECT lines matters; [HAPPY-EYEBALLS], [TCP], [MULTI] and [TIMER] are other tasks.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (a successful transfer and a refused connect), stderr in Notes; which `--trace-config` groups (`proxy`, `network`, `all`) turn the lines on is measured too.
- [x] Tests in `Curl.Networking.UnitTests` and `Curl.Console.UnitTests` pin the stable lines and that no line appears without its component; ADR-0357 gets an amendment for any volatile value.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for each library changed.

## Notes

- Measured 2026-10-02, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Tls`, `-s -k -v https://127.0.0.1:18443/`; fixtures in `%TEMP%\bl1192\<case>`.
  - `--trace-config https-connect`: `[HTTPS-CONNECT] added`, `connect, init`, `1st attempt uses h2 from wanted versions`, `Trying`, `connect -> 0, done=0` + `adjust_pollset -> 0, 1 socks`, the `schannel:` lines, `ALPN: curl offers http/1.1`, two more pairs, `ALPN: server did not agree...`, `connect -> 0, done=1`, `Established connection`, `removing connected setup filter`, `destroy`, `using HTTP/1.x`.
  - `--http1.1`: `1st attempt uses h1`. `--http2`: refused by this build (exit 2).
  - Refused (nothing listening): after `Trying`, three pairs (one per poll round over 2 s), `connect to ... failed`, `Failed to connect ...`, `connect, all attempts failed`, `connect -> 7, done=0`, `closing connection #0`.
  - Untrusted certificate (no `-k`): one pair after `ALPN: curl offers`, the `SEC_E_UNTRUSTED_ROOT` line, `connect, all attempts failed`, `connect -> 60, done=0`.
  - Components: `https-connect`, `all` and `-vvvv` write them; `network` and `proxy` do not. Plain `http://` writes none.
  - `[SETUP]` beside them (`https-connect,setup` and `all`): no `[SETUP] added` for an https:// origin; `[SETUP] happy eyeballing` after `1st attempt`; `[SETUP] added SSL filter for origin` after `[HAPPY-EYEBALLS] Connected to` and before the `schannel:` lines; removal order `[DNS]`, `[HTTPS-CONNECT]`, `[SETUP]`, `[HAPPY-EYEBALLS]`.
- Delivered (ADR-0357's BL-1192 amendment): `HttpsConnectFilterTraceEvents` (Networking), between the `[SETUP]` and `[DNS]` events of a direct connect to an https:// origin under `TcpConnector.TracesHttpsConnectFilter`; `HttpsConnectFirstAttemptVersion` names the version. The setup filter now skips `[SETUP] added` and writes `SslFilterAddedLine` for an https:// origin. `CurlComposition.TracesHttpsConnect` and `HttpsConnectFirstAttemptVersionOf` wire it.
- Decisions: the poll-round pairs are volatile, fixed to the loopback counts (one after `Trying`, two before a finished handshake's lines, one before a failed one's); the handshake's pairs come before its `ALPN:` lines, which one event carries (fixing that would need `Curl.Output`, outside `touches`). Under `dns` the `[DNS] Curl_conn_connect ... done=0` line precedes the first pair. A name that does not resolve writes only `added`.
- Live check: the Debug `curl.exe` through `Record-CurlExchange.ps1 -Curl` printed the success, refused and untrusted cases as above, except where the decisions say otherwise. It also writes no `SEC_E_UNTRUSTED_ROOT` line, which happened before this task too.
- Tests: `HttpsConnectFilterTraceEventsTests`, `TcpConnectorTests.HttpsConnectTrace` (setup order, DNS nesting, refused exit 7, handshake exit 60, unresolved, non-https targets, setup alone), `CurlCommandRunnerHttpsConnectTraceTests` (h2/h1 lines, `all` and `-vvvv`, none under `-v`, `-vvv`, `network`, `proxy` or `http://`, the version mapping). `Measure-CodeQuality.ps1`: Curl.Networking.UnitLibrary and Curl.Console 100% line, 100% branch, 0 failing members.
- Filed BL-1254 for proxies, Unix sockets and QUIC.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config https-connect, all and -vvvv write curl's [HTTPS-CONNECT] lines for a direct https:// connect
