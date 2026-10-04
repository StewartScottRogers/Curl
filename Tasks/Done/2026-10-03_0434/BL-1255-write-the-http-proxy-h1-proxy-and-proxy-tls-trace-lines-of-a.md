---
id: BL-1255
title: Write the [HTTP-PROXY], [H1-PROXY] and proxy TLS trace lines of a CONNECT tunnel through an HTTPS proxy
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1255 — Write the [HTTP-PROXY], [H1-PROXY] and proxy TLS trace lines of a CONNECT tunnel through an HTTPS proxy

## Goal

Curl writes curl 8.21.0's `[SETUP]`, `[HTTP-PROXY]` and `[H1-PROXY]` lines (and any proxy TLS filter lines its `--trace-config` groups name) around a CONNECT tunnel through an HTTPS proxy (`-x https://`), as BL-1193 does through a plain HTTP proxy.

## Context

- Split from BL-1193 (ADR-0357's BL-1193 amendment), which writes them through `Http` and `Http10` proxies only: `HttpProxyTunnelTrace` is created in `TcpConnector.OpenPlainTunnelAsync`; `OpenTunnelOverTlsAsync` passes none, and `TracesSetupFilterThrough` leaves the setup filter off for an `Https` proxy.
- Measure first with `Record-CurlExchange.ps1`; the recorder's `-Tls` mode serves TLS but not a CONNECT followed by the tunnel's bytes, so extend it to play an HTTPS proxy (TLS, then the `-Script` steps). Where `[SETUP] added SSL filter for proxy`, the proxy handshake's lines and `CONNECT: 'http/1.1' negotiated` fall relative to the tunnel lines is what matters.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (a successful transfer and a refused connect, `--trace-config proxy,setup` and `all`), stderr in Notes.
- [x] Tests in `Curl.Networking.UnitTests` pin the lines through an `Https` proxy and that none appears without its component; ADR-0357 gets an amendment for any volatile value.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for Curl.Networking.UnitLibrary.

## Notes

- Recorder: no extension was needed. `Record-CurlExchange.ps1 -Tls -Script` already serves TLS from the first byte and then runs the script's steps over it, so it plays an HTTPS proxy (script: `read`, `send HTTP/1.1 200 Connection established\r\n\r\n`, `read`, `send` a 200 with `Content-Length: 2`, `read`). Run it from PowerShell (`& .\Record-CurlExchange.ps1 ... -CurlArgs '-s','-v','--trace-config','proxy,setup',...`): under `-File` a `proxy,setup` value is split at its comma.
- Measured 2026-10-02, curl 8.21.0 mingw Schannel, `-s -v --trace-config proxy,setup -p --proxy-insecure -x https://127.0.0.1:18955 http://example.test/x`, exit 0 (head lines left out):

  ```
  * [SETUP] added
  * [SETUP] happy eyeballing to proxy 127.0.0.1:18955
  *   Trying 127.0.0.1:18955...
  * [SETUP] added SSL filter for HTTP proxy
  * [SETUP] added HTTP proxy tunnel filter
  * [HTTP-PROXY] CONNECT
  * [SSL-PROXY] cf_connect()
  * schannel: disabled automatic use of client certificate
  * schannel: using IP address, SNI is not supported by OS.
  * ALPN: curl offers http/1.1
  * [SSL-PROXY] cf_connect() -> 0, done=0
  * [SSL-PROXY] adjust_pollset, POLLIN fd=404
  * [HTTP-PROXY] CONNECT
  * [SSL-PROXY] cf_connect()
  * [SSL-PROXY] cf_connect() -> 0, done=0
  * [SSL-PROXY] adjust_pollset, POLLIN fd=404
  * [HTTP-PROXY] CONNECT
  * [SSL-PROXY] cf_connect()
  * ALPN: server did not agree on a protocol. Uses default.
  * [SSL-PROXY] cf_connect() -> 0, done=1
  * [SSL-PROXY] query ALPN
  * [SSL-PROXY] query ALPN: returning '(nil)'
  * CONNECT: no ALPN negotiated
  * [HTTP-PROXY] installing subfilter for HTTP/1.1
  ... (the plain tunnel's [H1-PROXY] and CONNECT lines, as in BL-1193 Notes) ...
  * [H1-PROXY] new tunnel state 'failed'
  * Established connection to 127.0.0.1 (127.0.0.1 port 18955) from 127.0.0.1 port 61268
  * [SETUP] removing connected setup filter
  * [SETUP] destroy
  * [HTTP-PROXY] removing connected setup filter
  * [HTTP-PROXY] destroy
  * [H1-PROXY] query ALPN
  * using HTTP/1.x
  ```
- `--trace-config http-proxy`: only the `[HTTP-PROXY]` lines (three `CONNECT` before `installing subfilter`, one more in the receive round, the removal). `--trace-config ssl`: no `[HTTP-PROXY]` or `[H1-PROXY]` line, but `ssl` turns on `[SSL-PROXY]` too. `--trace-config all`: the same order among many `[MULTI]`, `[TCP]`, `[SSLS]`, `[DNS]` and `[HAPPY-EYEBALLS]` lines; the two `[SETUP] added ...` lines follow `[HAPPY-EYEBALLS] Connected to`.
- Refused connect (`-x https://127.0.0.1:1`, `proxy,setup`, exit 7): `[SETUP] added`, `[SETUP] happy eyeballing to proxy 127.0.0.1:1`, `Trying`, `connect to 127.0.0.1 port 1 ... failed: Connection refused`, `Failed to connect to example.test:80 over proxy 127.0.0.1 after 2031 ms: Could not connect to server`; no tunnel or SSL line.
- An `https://` origin through it could not be measured with a script that closes after the 200 (curl waits on the closed TLS stream, exit 56). BL-1283's Context measured the `[SETUP]` lines on that path (`added SSL filter for origin` once the tunnel is open), and a test pins them.
- Decisions (ADR-0357's BL-1255 amendment): the setup filter is traced through an `Https` proxy too (`TracesSetupFilterThrough` removed), with `TcpConnector.HttpsProxySslFilterAddedLine` and `HttpProxyTunnelFilterAddedLine` before the proxy's handshake; `OpenTunnelOverTlsAsync` now carries an `HttpProxyTunnelTrace` like the plain tunnel. The handshake's poll rounds (two on loopback) are fixed and written by `HttpProxyTunnelTrace.ReportProxyHandshakePolled` once the handshake is done, so they follow the handshake's `-v` lines rather than sitting among them. A failed proxy handshake writes only the first `[HTTP-PROXY] CONNECT` (unmeasured). The `[SSL-PROXY]` lines are not written: no `[SSL]` filter line exists yet and the console has no `ssl` group flag (outside this task's touches), so they are filed as BL-1287, together with placing the poll lines among the handshake's.
- This also does BL-1283's `Curl.Networking` half (the `[SETUP]` lines through an HTTPS proxy, for an `http://` and an `https://` origin); its `Curl.Console.UnitTests` pins are still to do.
- Tests: `TcpConnectorTests.HttpsProxyTunnelTrace` (7). Fast tests all green; `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. A CONNECT tunnel through an HTTPS proxy writes curl's [SETUP], [HTTP-PROXY] and [H1-PROXY] lines
