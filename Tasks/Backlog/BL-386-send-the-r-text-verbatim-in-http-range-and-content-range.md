---
id: BL-386
title: Send the -r text verbatim in HTTP Range and Content-Range
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-386 — Send the -r text verbatim in HTTP Range and Content-Range

## Goal

HTTP `Range` and `Content-Range` carry the `-r` text as typed, as curl 8.21.0 does, so `-r 0-9,20-29` sends `Range: bytes=0-9,20-29` instead of `bytes=0-9`.

## Context

- Found in BL-306. Measured on curl 8.21.0 (mingw, Schannel) with `Record-CurlExchange.ps1`: `curl -d x -r 0-9,20-29 URL` sends `Content-Range: bytes 0-9,20-29/1`. libcurl passes `data->state.range` (the option text) straight into both headers.
- The handler only sees `ITransferContext.Range`, the one `ByteRange` that `Curl.Core/ByteRangeParser.cs` reads the way `Curl_range` does for `file://` (`2-3,5-6` becomes `2-3`, `1-2abc` becomes `1-2`). The HTTP handler formats that parsed range (`HttpRangeHeader`), so a list or trailing text is lost. ADR-0044 "Costs and caveats" records the gap.
- Needs the contract to carry the raw text (e.g. `ITransferContext.RangeText`) set by `Curl.Console`, and a measurement of what curl sends for odd text (`1-2abc`, `abc`, `-0`) over HTTP before pinning, since `ByteRangeParser`'s exit-33 cases were measured over `file://` only.

## Acceptance criteria

- [ ] `-r 0-9,20-29` sends `Range: bytes=0-9,20-29` on a GET and `Content-Range: bytes 0-9,20-29/1` with `-d x`, pinned in `HttpProtocolHandlerTests` from measured bytes.
- [ ] The HTTP behaviour of `-r 1-2abc`, `-r abc` and `-r -0` is measured against curl 8.21.0, recorded in Notes, and pinned.
- [ ] ADR-0044's caveat about the parsed range is removed or updated.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports no failing member for every library changed.

## Notes

## Log

- 2026-09-27: Created.
