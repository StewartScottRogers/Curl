---
id: BL-1168
title: Write the --trace-config http/3 lines from the HTTP/3 layer
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Http3.UnitLibrary, Curl.Http3.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1168 — Write the --trace-config http/3 lines from the HTTP/3 layer

## Goal

Under `-v --trace-config http/3` (and `protocol`, `all`) Curl writes the `* [HTTP/3] ...` lines curl 8.21.0 writes for an HTTP/3 transfer, from `Curl.Http3.UnitLibrary`.

## Context

- Split from BL-1104 (ADR-0318). Follow BL-1102's pattern: `Curl.Console` decides whether the component is on (`http/3`, `protocol` or `all`) and hands the HTTP/3 layer an `ITransferEvents` sink.
- Not measured yet: the reference Schannel build has no HTTP/3. Measure on an OpenSSL build of curl 8.21.0 with HTTP/3 (ngtcp2/nghttp3) against a local HTTP/3 server, run with `Record-CurlExchange.ps1 -NoServer`, and record the lines in Notes.

## Acceptance criteria

- [x] The `[HTTP/3]` lines of an HTTP/3 GET under `-v --trace-config http/3` are measured and recorded in Notes.
- [x] Tests pin them; `protocol` and `all` write the same; `-v` alone, another component and `http/3` without `-v` write none.
- [x] `--ai-help` still describes `--trace-config` correctly.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

**Measured** 2026-10-02 with curl.se's Windows build 8.18.0 (LibreSSL 4.2.1, ngtcp2 1.21.0,
nghttp3 1.15.0; `%LOCALAPPDATA%\Microsoft\WinGet\Links\curl.exe`), the HTTP/3 reference
ADR-0144 names; no 8.21.0 build with HTTP/3 is on this machine (the Schannel build has none).
No local HTTP/3 server exists, so against `https://cloudflare-quic.com/`, as BL-734 did:
`curl -s -v --trace-config http/3 --http3-only https://cloudflare-quic.com/ -o out.html`, exit 0,
611 stderr lines. With the I/O loop lines (`ingress`, `egress`, `vquic_send`, `vquic_recvfrom`,
`vquic_send_tail_split`, `cf_send`, `cf_recv`, `read_stream`) left out, the `[HTTP/3]` lines are:

```
*   Trying <ip>:443...
* [HTTP/3] configuring OpenSSL's x509 trust store
...TLS lines...
* [HTTP/3] handshake complete after 44ms, remote transport[max_udp_payload=65527, initial_max_data=10485760]
* [HTTP/3] max bidi streams now 100, used 0
* [HTTP/3] peer verified
* [HTTP/3] connect -> 0, done=1
* Established connection to cloudflare-quic.com (<ip> port 443) from <ip> port <port>
* using HTTP/3
* [HTTP/3] peer idle timeout is 180000ms, set keep-alive to 90000 ms.
* [HTTP/3] [0] OPENED stream for https://cloudflare-quic.com/      <- also under -v alone (BL-734)
* [HTTP/3] [0] [:method: GET] ... [accept: */*]                    <- also under -v alone
> GET / HTTP/3 ...
* Request completely sent off
* [HTTP/3] [3] read_stream(len=24) -> 24     (server's control/QPACK streams, I/O loop)
* [HTTP/3] [15] quic close(app_error=256) -> 0
< HTTP/3 200
* [HTTP/3] [0] status: HTTP/3 200
                                              <- (an empty line: the status text ends in a newline)
* [HTTP/3] [0] header: date: <date>
< date: <date>
... (header: echo before each < line)
<
* [HTTP/3] [0] end_headers, status=200
{ [993 bytes data]
* [HTTP/3] [0] DATA len=993
* [HTTP/3] [0] ACK 993/993 bytes of DATA
... (one DATA/ACK pair after each { line)
* [HTTP/3] [0] CLOSED
* [HTTP/3] [0] quic close(app_error=256) -> 0
{ [0 bytes data]
* [HTTP/3] [0] easy handle is done
* [HTTP/3] no active streams, unset keep-alive
* [HTTP/3] query conn[0]: MAX_CONCURRENT -> 99 (0 in use)
* Connection #0 to host cloudflare-quic.com:443 left intact
```

**Done here** (ADR-0375): `Http3StreamTrace` in `Curl.Protocol.Http.UnitLibrary` writes the
stream lines - `end_headers, status=<code>` for each head, `DATA len` / `ACK` per piece of body,
`CLOSED` and `quic close(app_error=256) -> 0` at the end - holding each until the stream is next
read, so they land after the `<` and `{` lines as in curl. `HttpProtocolHandler.TracesHttp3Streams`
turns it on; `CurlComposition.TracesHttp3` sets it for `http/3`, `protocol`, `all` (so `-vv`).
The console writes info lines only under `-v` (pinned by
`RunAsync_TraceConfigReadWithoutVerbose_WritesNothing`), so `http/3` without `-v` writes none.

**Touches widened** to `Curl.Protocol.Http.UnitLibrary` and `.UnitTests`: the HTTP/3 stream is
`Http3StreamConnection` there; `Curl.Http3.UnitLibrary` holds only frames and QPACK, so it is
unchanged. No other task in Doing on `origin/work/dark-factory` named either (only BL-1204, SSH).

**Not done here, filed as BL-1208:** the connection lines (handshake, peer verified, connect,
idle timeout, stream limit, easy handle done, keep-alive, MAX_CONCURRENT query) and the
`status:` / `header:` echoes. The I/O loop lines are left out for good, as ADR-0373 did for HTTP/2.

`--ai-help` is built from the help table, whose `--trace-config` line ("Details to log in
trace/verbose output") names no component and stays correct.

Tests: `HttpProtocolHandlerTests.Http3Trace.cs` (3) and `CurlCompositionHttp3TraceTests` (13 cases).

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. --trace-config http/3 writes curl's [HTTP/3] end_headers, DATA/ACK, CLOSED and quic close stream lines in curl's places
