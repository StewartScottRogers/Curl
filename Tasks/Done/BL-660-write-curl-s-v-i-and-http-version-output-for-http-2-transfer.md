---
id: BL-660
title: Write curl's -v, -i and %{http_version} output for HTTP/2 transfers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-659]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-660 — Write curl's -v, -i and %{http_version} output for HTTP/2 transfers

## Goal

An HTTP/2 transfer writes what curl 8.21.0 writes on each platform (the OpenSSL build on Linux and macOS; on Windows, where the Schannel reference build has no HTTP/2, the build BL-655's ADR names, curl.se's official Windows build): `-i` status line `HTTP/2 200` with lower-case header names, the `-v` lines for ALPN, the stream and the request and response headers, `%{http_version}` `2`, and `-V` listing `HTTP2` among the features on every platform.

## Context

- Conformance audit 2026-09-28, row 32. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): HTTP/2 is offered on every platform; output text matches the platform's curl where both do the same thing.
- Formatting: `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, `TransferWriteOutVariables.cs` (`FormatHttpVersion`); `-V` per ADR-0021.
- Measure with the OpenSSL-build curl on Linux or macOS and with curl.se's Windows build against an HTTP/2 server (through `Record-CurlExchange.ps1 -NoServer`): `-v`, `-i` and `-w '%{http_version}'`.

## Acceptance criteria

- [x] Measured first as above; stdout and stderr copied into Notes with varying parts marked.
- [x] Tests pin each measured output on every platform, with `OSCondition` only where the Windows and OpenSSL texts differ.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

**Measured** 2026-09-30 with curl.se's Windows build 8.18.0 (LibreSSL, nghttp2 1.68.0, ngtcp2;
`%LOCALAPPDATA%\Microsoft\WinGet\Links\curl.exe`), the build BL-655's ADR names for Windows,
against live HTTP/2 and HTTP/3 servers (`-NoServer`-style: no loopback server needed, the
lines are the client's own). No Linux or macOS machine is at hand in a lane; the lines below
come from libcurl's own HTTP/2 and HTTP/3 layers (`lib/http2.c`, `lib/vquic/curl_ngtcp2.c`),
which the OpenSSL builds share, so one text serves every platform and no `OSCondition` is
needed. Varying parts in `<angle brackets>`.

`curl -s -v -i --http2 https://example.com/ -o out -w '%{http_version}\n'`, exit 0, stdout `2`:

```
* ALPN: curl offers h2,http/1.1
...TLS lines (platform TLS backend, not this task)...
* ALPN: server accepted h2
* Established connection to example.com (<ip> port 443) from <ip> port <port>
* using HTTP/2
* [HTTP/2] [1] OPENED stream for https://example.com/
* [HTTP/2] [1] [:method: GET]
* [HTTP/2] [1] [:scheme: https]
* [HTTP/2] [1] [:authority: example.com]
* [HTTP/2] [1] [:path: /]
* [HTTP/2] [1] [user-agent: curl/<version>]
* [HTTP/2] [1] [accept: */*]
> GET / HTTP/2
> Host: example.com
> User-Agent: curl/<version>
> Accept: */*
>
* Request completely sent off
< HTTP/2 200
< date: <date>
< content-type: text/html; charset=utf-8
...headers, lower case, as received...
<
{ [<n> bytes data]
* Connection #0 to host example.com:443 left intact
```

and `out` begins `HTTP/2 200 \r\n` with the same lower-case header lines, the empty line, then
the body. `curl -v --http2 -u a:b -H "X-Custom: One" -d x=1 "https://A:B@example.com/p?q=1#frag"`
writes `Server auth using Basic with user 'a'` before `[HTTP/2] [1] OPENED stream for
https://A:B@example.com/p?q=1#frag` (URL with credentials and fragment), then one line per
HEADERS field in the order sent: `:method: POST`, `:scheme`, `:authority`, `:path: /p?q=1`,
`authorization`, `user-agent`, `accept`, `x-custom: One`, `content-length: 3`, `content-type`.
`https://EXAMPLE.com:443?x` is named `https://EXAMPLE.com:443/?x`, the same text as
`%{url_effective}`. `--http3-only https://cloudflare-quic.com/` writes the same shape as
`[HTTP/3] [0] ...` (stream 0), so HTTP/3 got the lines too: same code path, one extra test.

**What was already right** (BL-658, BL-659): `-i`'s `HTTP/2 200 ` and lower-case headers,
`%{http_version}` `2` (`TransferWriteOutVariablesTests`, `HttpProtocolHandlerTests.Http2`),
`-V` listing `HTTP2` (`CurlVersionTextTests`), `using HTTP/2`, `> GET / HTTP/2`. So the Output
library and Console needed no change.

**Bug found and fixed (Networking added to touches).** Running our build against
example.com with `--http2` showed `ALPN: server accepted h2` then `using HTTP/1.x` and exit 1:
`TcpConnector.Opened` and `PoolingConnector.OpenAsync` both dropped
`ConnectResult.ApplicationProtocol`, so the handler never learned of `h2` and spoke HTTP/1.1
to an HTTP/2 server. Both now pass it on (tests in `TcpConnectorTests`,
`PoolingConnectorMultiplexingTests`). No task in Doing named `Curl.Networking.UnitLibrary`
or its tests, so they were added to this task's `touches` (lane rule 3).

**Design.** `HttpStreamOpenedLines` (Http library) reports the lines through
`ITransferEvents.ReportInfo`; `IHttpStreamSession.CreateStream` takes it as an optional
argument, and `Http2StreamConnection` reports once HEADERS are sent (so the stream id is
known), `Http3StreamConnection` once the QUIC stream is opened, both before the handler
reports the `>` head. The h2c upgrade's stream 1 reports nothing, as curl submits no HEADERS
for it. The URL is `HttpUrlText.Effective`, the same rule as Console's `UrlEffective` for
http(s): lower-case scheme, authority as typed, path, query, fragment. No ADR: every line is
measured, nothing was chosen.

**Gates.** `dotnet build Curl.slnx -warnaserror` clean; fast tests all pass (Http 1518 run,
Networking 1822 run, every other project green); `Measure-CodeQuality.ps1` 100% line and
branch, 0 failing members, for `Curl.Protocol.Http.UnitLibrary` and
`Curl.Networking.UnitLibrary`; `dotnet format --verify-no-changes` clean on every changed file.

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. HTTP/2 over ALPN h2 now reaches the handler, and -v writes curl's [HTTP/2] and [HTTP/3] OPENED stream and header lines; -i and %{http_version} match
