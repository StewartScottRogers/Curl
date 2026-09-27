---
id: BL-328
title: Let SOCKS and HTTPS-proxy tunnels reach the connector from Curl.Console once it opens them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-213, BL-266]
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-328 — Let SOCKS and HTTPS-proxy tunnels reach the connector from Curl.Console once it opens them

## Goal

`--socks4`, `--socks4a`, `--socks5`, `--socks5-hostname`, `socks*://` and `https://` proxies that need a tunnel reach the connector from `curl http(s)://...` instead of exiting 4.

## Context

- Filed by BL-238 (2026-09-27). `Curl.Console/TransferProxySelection.cs` ends an `http`/`https` transfer with exit 4 `Unsupported proxy '<host>:<port>', Curl cannot tunnel through a <kind> proxy yet` when the route needs a SOCKS tunnel or an HTTPS-proxy tunnel, because `TcpConnector` throws `NotSupportedException` for those kinds (ADR-0053).
- Once BL-213 (SOCKS) and BL-266 (HTTPS proxy) land in `Curl.Networking`, remove `TunnelNotBuiltYetFailure` and its tests in `CurlCompositionProxyTests`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH (ADR-0009) - against a loopback server (`Record-CurlExchange.ps1`), record the exact command and the bytes in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] `TransferProxySelection` no longer returns exit 4 for any proxy kind; the exit-4 rows in `CurlCompositionProxyTests` are replaced by rows pinning the measured SOCKS5 greeting (`05 02 00 01` for `--socks5`, BL-192 Notes) and the HTTPS-proxy CONNECT bytes over the fake connector.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for every library this task changes.

## Notes

- Plan: BL-213 and BL-266 are Done and `TcpConnector` now tunnels through every `ProxyKind`, and `HttpProtocolHandler` already hands SOCKS and tunnelled HTTPS routes to the connector, so the whole change is deleting `TunnelNotBuiltYetFailure`, `NeedsTunnelNotBuiltYet`, `IsHttpProxy` and `IsForwardedOverTls` from `TransferProxySelection` and replacing the exit-4 tests. The pipeline's plan and review stages were not delegated: the change removes code only.
- Measured, curl 8.21.0 mingw (`/mingw64/bin/curl`), 2026-09-27, against a loopback SOCKS5 proxy (Python, 127.0.0.1:18328) answering `05 00`, then `05 00 00 01 7f 00 00 01 00 50`, then `HTTP/1.1 200 OK` with `hello`:
  - `curl -sS --socks5 127.0.0.1:18328 http://example.com/a` sent `05 02 00 01`, `05 01 00 01 <example.com's resolved IPv4> 00 50`, then `GET /a HTTP/1.1\r\nHost: example.com\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`; exit 0, output `hello`. The test pins the address the fake resolver gives, 127.0.0.1.
  - `curl -sS --socks5-hostname 127.0.0.1:18328 http://example.com/a` sent `05 02 00 01`, `05 01 00 03 0b example.com 00 50`, then the same GET; exit 0.
- HTTPS-proxy CONNECT bytes are the ones BL-266 measured (`curl -s -S --proxy-insecure -x https://localhost:18405 https://example.com/` and `-p -x https://localhost:18411 http://example.com/`): `CONNECT example.com:<port> HTTP/1.1\r\nHost: example.com:<port>\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n`, pinned in `RunAsync_HttpsProxyTunnel_SendsConnectOverTlsThenTheRequestThroughTheTunnel` over `PassThroughTlsProvider`.
- Touches widened to `Documentation/Planning/Decisions`: ADR-0053 described the guard this task removes, so its status is now "Retired" (there is no new decision, so no superseding ADR). No task in Doing names that folder.
- Results: `dotnet build -warnaserror` clean; fast run green (Curl.Console.UnitTests 603 passed). `Measure-CodeQuality.ps1 -Library Curl.Console` in fast mode flags only `DiskWriteOutFileOpener.TryOpen`, which was there before (its tests are Integration, BL-311 Notes); with `-IncludeIntegration` Curl.Console is 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. curl --socks4/4a/5/5h, socks*:// and tunnelled https:// proxies reach TcpConnector instead of exiting 4
