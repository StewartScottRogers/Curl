---
id: BL-1399
title: Write curl's 'Ignoring Content-Length in CONNECT 200 response' lines and refuse a non-numeric CONNECT reply Content-Length with exit 8
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1394]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: FR-091
created: 2026-10-03
completed:
---
# BL-1399 — Write curl's 'Ignoring Content-Length in CONNECT 200 response' lines and refuse a non-numeric CONNECT reply Content-Length with exit 8

## Goal

Reading a proxy's reply to `CONNECT` (and to `CONNECT-UDP`), Curl writes curl 8.21.0's `* Ignoring Content-Length in CONNECT <code> response` / `* Ignoring Transfer-Encoding in CONNECT <code> response` info line after such a header of a 2xx reply, and fails a reply that does not open the tunnel whose `Content-Length` is not a number with exit 8 `Unsupported Content-Length value`.

## Context

- Today `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs` `ParseReply` (around line 190) records `Content-Length` with `long.TryParse(...) ? length : contentLength`, silently keeping 0 for `abc`, and writes no line for a 2xx reply's `Content-Length` or `Transfer-Encoding`; neither text exists in the solution.
- curl 8.21.0 (tag `curl-8_21_0`), `lib/cf-h1-proxy.c`:
  - `CONNECT`, lines 412-436: for `Content-Length:` when `k->httpcode / 100 == 2`, `infof(data, "Ignoring Content-Length in CONNECT %03d response", k->httpcode)` (RFC 9110 section 9.3.6: a client must ignore these fields in a 2xx reply to CONNECT); otherwise `curlx_str_numblanks` must read a number or `failf(data, "Unsupported Content-Length value")` and `CURLE_WEIRD_SERVER_REPLY`. `Transfer-Encoding:` on a 2xx gives `infof(data, "Ignoring Transfer-Encoding in CONNECT %03d response", k->httpcode)`.
  - `CONNECT-UDP`, lines 325-345: the same for a 2xx or `101` reply, worded `Ignoring Content-Length in CONNECT-UDP %03d response` and `Ignoring Transfer-Encoding in CONNECT-UDP %03d response`.
- Measured 2026-10-03 with curl 8.21.0 (mingw, Schannel) and `Record-CurlExchange.ps1` as the proxy, `-sv -p -x http://127.0.0.1:PORT http://example.com/a`:
  - reply `HTTP/1.1 200 Connection established\r\nContent-Length: 5\r\n\r\n`: `< HTTP/1.1 200 Connection established`, `< Content-Length: 5`, `* Ignoring Content-Length in CONNECT 200 response`, `< `, `* CONNECT phase completed for HTTP proxy`, `* CONNECT tunnel established, response 200`, then the GET through the tunnel (no body is read from the reply).
  - reply with `Transfer-Encoding: chunked` instead: `* Ignoring Transfer-Encoding in CONNECT 200 response` after that header line, the same sequence after it.
  - reply `HTTP/1.1 299 Fine\r\nContent-Length: 5\r\n\r\n`: `* Ignoring Content-Length in CONNECT 299 response`.
  - reply `HTTP/1.1 407 Proxy Auth\r\nContent-Length: abc\r\n\r\n`: `< Content-Length: abc`, `* Unsupported Content-Length value`, `* closing connection #0`; exit 8.
- BL-1394 changes this library first; build on it.

## Acceptance criteria

- [ ] Tests in `Curl.Networking.UnitTests` drive `HttpProxyTunnel` with each measured reply and pin the info line right after its header line and before the empty line, with the tunnel opened for the 2xx replies and no reply body read.
- [ ] A test pins the 407 with `Content-Length: abc`: exit 8 (`CurlExitCode.WeirdServerReply`), message `Unsupported Content-Length value`, the line after the header line; a 407 with `Content-Length:  12 ` (blanks) is still read as 12.
- [ ] Tests pin the `CONNECT-UDP` wording for a `101` and a `200` reply carrying `Content-Length` and `Transfer-Encoding`.
- [ ] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean; `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no failing member in the code this task changed.

## Log

- 2026-10-03: Created.
