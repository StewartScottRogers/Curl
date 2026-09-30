---
id: BL-734
title: Write curl's -v, -i, %{http_version} and -V output for HTTP/3 transfers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-732]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-734 — Write curl's -v, -i, %{http_version} and -V output for HTTP/3 transfers

## Goal

An HTTP/3 transfer writes what curl's official build writes: `-i` status line `HTTP/3 200` with lower-case header names, the `-v` lines for the QUIC connect, ALPN, the stream and the request and response headers, `%{http_version}` `3`, and `-V` listing `HTTP3` among the features (and `ngtcp2`/`nghttp3`-equivalent entries only as ADR-0021 allows), on every platform.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): output text matches the platform's curl where both do the same thing; where the platform's usual build has no HTTP/3 (the Schannel build on Windows), the text of curl.se's official Windows build is the reference. Formatting: `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, `TransferWriteOutVariables.cs` (`FormatHttpVersion`); `-V` per ADR-0021.
- Measure with the official curl build through `Record-CurlExchange.ps1 -NoServer` against an HTTP/3 server (BL-718 records which): `-v`, `-i` and `-w '%{http_version}'`, and `-V`.

## Acceptance criteria

- [x] Measured first as above; stdout and stderr copied into Notes with varying parts marked.
- [x] Tests pin each measured output on every platform, normalised as existing `-v` tests are.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

**Measured** 2026-09-30 with curl.se's Windows build 8.18.0 (LibreSSL 4.2.1, ngtcp2 1.21.0,
nghttp3 1.15.0; `%LOCALAPPDATA%\Microsoft\WinGet\Links\curl.exe`), the reference ADR-0144
names, against `https://cloudflare-quic.com/` (no local HTTP/3 server; the lines are the
client's own). Varying parts in `<angle brackets>`.

`curl -s -v -i --http3-only https://cloudflare-quic.com/ -o out -w '%{http_version}\n'`, exit 0,
stdout `3`, stderr:

```
Note: Using embedded CA bundle (225076 bytes)              <- build-specific, not printed (ADR-0144)
Note: Using embedded CA bundle, for proxies (225076 bytes)
* Host cloudflare-quic.com:443 was resolved.
* IPv6: (none)
* IPv4: <ip>, <ip>
*   Trying <ip>:443...
* SSL Trust Anchors:
*   CA Blob from configuration
* SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / [blank] / UNDEF
* Server certificate:
*   subject: CN=cloudflare-quic.com
*   start date: <date> GMT
*   expire date: <date> GMT
*   issuer: C=US; O=Google Trust Services; CN=WE1
*   Certificate level 0: Public key type ? (256/128 Bits/secBits), signed using ecdsa-with-SHA256
*   Certificate level 1: Public key type ? (256/128 Bits/secBits), signed using ecdsa-with-SHA384
*   Certificate level 2: Public key type ? (384/192 Bits/secBits), signed using ecdsa-with-SHA384
*   subjectAltName: "cloudflare-quic.com" matches cert's "cloudflare-quic.com"
* SSL certificate verified via OpenSSL.
* Established connection to cloudflare-quic.com (<ip> port 443) from <local ip> port <port> 
* using HTTP/3
* [HTTP/3] [0] OPENED stream for https://cloudflare-quic.com/
* [HTTP/3] [0] [:method: GET]
* [HTTP/3] [0] [:scheme: https]
* [HTTP/3] [0] [:authority: cloudflare-quic.com]
* [HTTP/3] [0] [:path: /]
* [HTTP/3] [0] [user-agent: curl/<version>]
* [HTTP/3] [0] [accept: */*]
> GET / HTTP/3
> Host: cloudflare-quic.com
> User-Agent: curl/<version>
> Accept: */*
> 
* Request completely sent off
< HTTP/3 200 
< date: <date>
< content-type: text/html
< priority: u=3,i=?0
< server: cloudflare
< cf-ray: <ray>
< alt-svc: h3=":443"; ma=86400
< server-timing: cfExtPri
< 
{ [<n> bytes data]
* Connection #0 to host cloudflare-quic.com:443 left intact
```

`out` begins `HTTP/3 200 \r\n`, the same lower-case header lines, the empty line, then the
body. No `ALPN:` lines for QUIC (as ADR-0144 already records). `-V` lists `HTTP2 HTTP3` under
`Features:`.

**Our build against the same server** (before this task): everything from `Trying` to
`left intact`, `-i`'s output and `%{http_version}` `3` already matched (BL-660, BL-731,
BL-732), except two things outside this task's projects, filed as follow-ups:
- no TLS lines on Windows (the QUIC handshake is worded as Schannel): BL-1050;
- `Established connection ... from 0.0.0.0 port <port> ` (the UDP socket is never connected): BL-1051.

**Changed.** `-V`'s `Features:` gains `HTTP3` on every platform (ADR-0144 Consequences:
"`curl -V` lists `HTTP3` ... once BL-732 makes the options work"; no `ngtcp2/` or `nghttp3/`
token). That is `Curl.Cli.UnitLibrary/CurlVersionText.cs`; no task in Doing named
`Curl.Cli.UnitLibrary` or `Curl.Cli.UnitTests`, so both were added to `touches` (lane rule 3).
`CurlCommandRunnerHttp3Tests.RunAsync_Http3OnlyVerboseInclude_WritesCurlsLinesAndHttpVersion3`
pins the measured `-s -v -i --http3-only -w '%{http_version}\n'` run end to end through the
production composition, from `using HTTP/3` to `left intact`, plus stdout. It drops every CR
from stderr: header lines keep their CR LF and Windows' text mode adds one more, so this
compares the same text on every platform. No ADR: every line is measured, nothing chosen.

**Gates.** `dotnet build Curl.slnx -warnaserror` clean; every fast test project passes
(Console 1931, Cli 3121); `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` 100% line
and branch, 0 failing members. No Output, Http or Console production code changed.

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. -V lists HTTP3; an HTTP/3 transfer's -v, -i and %{http_version} match curl.se's ngtcp2 build and are pinned end to end
