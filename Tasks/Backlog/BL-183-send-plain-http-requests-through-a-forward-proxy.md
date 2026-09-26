---
id: BL-183
title: Send plain-HTTP requests through a forward proxy
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-173, BL-162]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-183 — Send plain-HTTP requests through a forward proxy

## Goal

With a forward proxy the handler connects to the proxy and sends an absolute-form request target with Proxy-Authorization and the headers curl adds, while https via a proxy is left to the connector's tunnel.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H15. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `ProxyEndpoint` and `ConnectTarget.Proxy` (BL-162); CONNECT tunnelling is BL-212 in `Curl.Networking`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] An http URL through a proxy sends `GET http://host/path HTTP/1.1` with Proxy-Authorization and any extra proxy headers byte-equal to curl 8.21.0 (measured).
- [ ] An https URL through a proxy passes `ConnectTarget.Proxy` to the connector and sends an origin-form request over the returned connection.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H15 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
