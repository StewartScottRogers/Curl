---
id: BL-1415
title: Carry --proxy-http2 from the parsed options to the connector in Curl.Console (ADR-0408)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1416, BL-1414]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1415 — Carry --proxy-http2 from the parsed options to the connector in Curl.Console (ADR-0408)

## Goal

Off Windows, `curl --proxy-http2 -x https://proxy ...` reaches the connector with proxy HTTP/2 requested, so the transfer runs through an HTTP/2 tunnel end to end.

## Context

- ADR-0408 decision 5. BL-1416 parses the option in `Curl.Cli.UnitLibrary`, and BL-1414 adds the connector option in `Curl.Networking.UnitLibrary`.
- `Curl.Console` builds the connect request from the parsed options; start at `HttpVersionMapping.cs` and the connector set-up.

## Acceptance criteria

- [x] A `Curl.Console.UnitTests` test shows the parsed proxy-HTTP/2 setting reaches the connector option, and that leaving it out leaves the option off.
- [x] A test over a fake connector runs a transfer through the HTTP/2 tunnel.
- [x] `--ai-help` still describes `--proxy-http2` correctly.
- [x] `Curl.Console` keeps 100% line and branch coverage. `dotnet build` is clean and the fast tests are green.

## Notes

- `CurlComposition.CreateProxyTunnelOptions` now sets `HttpProxyTunnelOptions.ProxyHttp2 = options.ProxyHttp2`. Nothing else in Curl.Console needed changing, because the run's `TcpConnector` is already built with those tunnel options (`CurlTransports`).
- The tests are in `Curl.Console.UnitTests/CurlCompositionProxyTests.Http2Tunnel.cs`. They parse with `isWindows: false` (the OpenSSL build), because the Schannel build refuses `--proxy-http2` (BL-1416), so they pass on every platform. The transfer test builds a real `TcpConnector` from the mapped options. It runs over a scripted dialer and a TLS provider that agrees `h2`, then checks the HTTP/2 `CONNECT` HEADERS (`:authority: example.com:80`) and that the tunnelled `GET` goes out in DATA on stream 1.
- `--ai-help` gets `--proxy-http2` from `CurlHelpTable` ("Use HTTP/2 with HTTPS proxy", Http|Proxy). This task does not change it, and it is still correct.
- `Measure-CodeQuality -Library Curl.Console`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. --proxy-http2 now reaches the connector from Curl.Console, so an h2 HTTPS proxy tunnels the transfer over an HTTP/2 CONNECT stream
