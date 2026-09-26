---
id: BL-212
title: Tunnel through an HTTP proxy with CONNECT in the connector
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-162]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-212 — Tunnel through an HTTP proxy with CONNECT in the connector

## Goal

When `ConnectTarget.Proxy` is an HTTP proxy with tunnelling, the connector sends CONNECT, checks the answer, then runs TLS over the tunnel.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item N2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured: `-p -x` answered 407 gives exit 7 `curl: (7) CONNECT tunnel failed, response 407`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] CONNECT request bytes are byte-equal to curl 8.21.0 (measured), including Proxy-Authorization.
- [x] A non-2xx answer returns `CurlExitCode.CouldntConnect` (7) `CONNECT tunnel failed, response N`; an unresolvable proxy returns `CouldntResolveProxy` (5) with the measured message.
- [x] TLS runs over the tunnel for an https target.
- [x] `dotnet build Curl.Networking.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking`. (Every member this task added or changed passes, and so do the pre-existing `OpenSslCipherSuites.Select`, refactored here, and `TcpDialer`/`UdpDatagramChannel` with `-IncludeIntegration`. One pre-existing member still fails on Windows: `SslStreamTlsProvider.CreateCipherSuitesPolicy` line 227 runs only off Windows. That is filed as BL-260; see Notes.)

## Notes

- Plan item: N2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- **Touches widened** to `Documentation/Planning/Decisions` for ADR-0023 and its index row. No task in Doing names it.
- **Decisions** (ADR-0023, decided by Claude under Stewart's delegation):
  - A set `ConnectTarget.Proxy` always means tunnel.
  - `User-Agent` and credential encoding come from a new optional `HttpProxyTunnelOptions` constructor argument. The default is `curl/8.21.0` and UTF-8.
  - The reply is read one byte at a time, so the tunnel's bytes stay on the connection.
  - HTTPS and SOCKS proxy kinds throw `NotSupportedException`, so the connector never silently goes direct.
- **Measured** on 2026-09-26 with `/mingw64/bin/curl` 8.21.0 (Schannel), `Record-CurlExchange.ps1` acting as the proxy, arguments passed from PowerShell:
  - `-p -x 127.0.0.1:18261 http://example.com/`, answered 407: sent `CONNECT example.com:80 HTTP/1.1
Host: example.com:80
User-Agent: curl/8.21.0
Proxy-Connection: Keep-Alive

`; `curl: (7) CONNECT tunnel failed, response 407`.
  - `-x http://127.0.0.1:18262 -U user:p@ss https://example.com:8443/path`: `Proxy-Authorization: Basic dXNlcjpwQHNz` between `Host` and `User-Agent`.
  - `--proxy-user u2:p2` given after `-U user:pw`: the last one wins (`dTI6cDI=`).
  - `-p -x ... http://[::1]:81/`, answered 403: `CONNECT [::1]:81`, `Host: [::1]:81`; response 403.
  - `--proxy1.0 127.0.0.1:18264 -p`: `HTTP/1.0` on the request line, same headers.
  - `-A Agent/1`: `User-Agent: Agent/1`.
  - Replies 300 and `garbage` give exit 7 with response 300 and response 0. `HTTP/1.1 2000 OK`, and `HTTP/1.1` followed by `X: 200`, both give response 0. A 299 reply opens the tunnel.
  - No reply, or a header block cut short, gives `curl: (56) Proxy CONNECT aborted`.
  - A reply line of 16384 bytes or more, CR and LF included, gives `curl: (56) CONNECT response too large`. 16383 bytes are read.
  - 1200 lines of 1009 bytes after the status line give `curl: (56) Too large response headers: 307762 > 307200`, checked at each line end.
  - `-x no-such-proxy.invalid:3128`: `curl: (5) Could not resolve proxy: no-such-proxy.invalid`.
  - `-p -x localhost:1 http://example.com:8080/`: `curl: (7) Failed to connect to example.com:8080 over proxy localhost after 2268 ms: Could not connect to server`.
  - With target `http://[::1]:8080/`: `Failed to connect to ::1:8080 over proxy ...`, with the IPv6 address unbracketed.
- **Code review** (code-reviewer) findings:
  - Fixed: the proxy connection is now disposed when sending or reading CONNECT throws.
  - Fixed: the measured size limits are applied.
  - Fixed: the status line is parsed from the first line only, with exactly three digits.
  - Left: a CR/LF in the host or User-Agent is not guarded. Hosts come from `Uri`, and `-A` wiring is BL-259.
- **Gate:** `OpenSslCipherSuites.Select` (complexity 12, pre-existing) was brought under 10 by extracting `ParseListOrDefault`. Its behaviour is unchanged.
- **Follow-ups filed:**
  - BL-258: HTTPS proxy.
  - BL-259: `Curl.Console` passes `-A` and `CredentialEncoding.ForPlatform` (ADR-0022) to the tunnel. Until then the default encoding is UTF-8, which differs from the Windows ANSI code page for non-ASCII credentials.
  - BL-260: the Windows-unreachable coverage line.
  - SOCKS was already BL-213.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. TcpConnector tunnels through HTTP and HTTP/1.0 proxies with CONNECT (bytes and exit 5/7/56 messages as measured on curl 8.21.0) and runs TLS over the tunnel
