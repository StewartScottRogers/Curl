---
id: BL-308
title: Send TE: gzip for --tr-encoding and decode a gzip Transfer-Encoding in the HTTP handler
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-180]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-308 — Send TE: gzip for --tr-encoding and decode a gzip Transfer-Encoding in the HTTP handler

## Goal

With `--tr-encoding`, the HTTP handler sends `TE: gzip` and `Connection: TE` and decodes a response whose Transfer-Encoding lists `gzip` (and `deflate`), as curl 8.21.0 does.

## Context

- Split out of BL-180 (2026-09-26): `HttpRequestOptions` has no member for `--tr-encoding`, so the handler cannot see it. Add one (e.g. `bool TransferEncoding`) to `Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs`; `CommandLineOptions` already parses the option (BL-191). BL-236 wires it in Curl.Console.
- Measured on curl 8.21.0 (mingw, `/mingw64/bin/curl`) with `Record-CurlExchange.ps1 -Port 18180 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello' -CurlArgs '--tr-encoding','http://127.0.0.1:18180/a'`: the request was `GET /a HTTP/1.1`, `Host: 127.0.0.1:18180`, `User-Agent: curl/8.21.0`, `Accept: */*`, `TE: gzip`, `Connection: TE`, then the empty line; stdout `hello`, exit 0.
- Without `--tr-encoding`, `Transfer-Encoding: gzip` is exit 61 (`HttpTransferEncoding`, BL-171). Still to measure: a gzip and a `gzip, chunked` Transfer-Encoding with `--tr-encoding`, `-H "Connection: x"` alongside it, and `--raw --tr-encoding`.
- Upstream: https://curl.se/docs/manpage.html#--tr-encoding

## Acceptance criteria

- [ ] `HttpRequestOptions` carries `--tr-encoding`, and a `Curl.Protocol.Abstractions.UnitTests` test pins its default (off).
- [ ] The request bytes above are pinned in a `Curl.Protocol.Http.UnitTests` test, and every `--tr-encoding` response case listed in Context is measured on curl 8.21.0, recorded in Notes and pinned, with 1-byte reads too.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http` and `Curl.Protocol.Abstractions`.

## Notes

## Log

- 2026-09-26: Created.
