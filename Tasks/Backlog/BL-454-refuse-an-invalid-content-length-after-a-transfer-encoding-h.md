---
id: BL-454
title: Refuse an invalid Content-Length after a Transfer-Encoding header under --tr-encoding
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-454 — Refuse an invalid Content-Length after a Transfer-Encoding header under --tr-encoding

## Goal

Under `--tr-encoding`, a response whose invalid Content-Length comes after a Transfer-Encoding header fails with exit 8 `Invalid Content-Length: value`, as curl 8.21.0 does, instead of being accepted.

## Context

- Measured in BL-412 (2026-09-27), curl 8.21.0 mingw with `Record-CurlExchange.ps1`: `-sS --tr-encoding -D -` against `HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\nContent-Length: x\r\nX-After: 1\r\n\r\n0\r\n\r\n` gives exit 8 `Invalid Content-Length: value`, header output `HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n`. Without `--tr-encoding`, and with `--raw`, it is the same, and Curl already matches those.
- Curl's `HttpResponseBodyFraming.OfRequested` checks only the Content-Length headers before the first Transfer-Encoding header (BL-315 Notes: "one after it is not read"), so it accepts this response with exit 0. BL-315 presumably measured a valid but disagreeing value, which curl does not trust; an invalid one is still parsed and refused (curl sets its ignore-Content-Length flag, but still parses the value).
- Measure first: a valid Content-Length after the Transfer-Encoding (expected accepted), two disagreeing valid ones after it, and `--ignore-content-length --tr-encoding`.
- `HttpResponseBodyReader.FindHeadRefusal` (BL-412) finds the refused header by checking runs of headers with `HttpResponseBodyFraming.Of`, so the fix there also cuts header output at the right place.

## Acceptance criteria

- [ ] The case above and the cases still to measure are measured, recorded in Notes and pinned in `Curl.Protocol.Http.UnitTests` with 1-byte reads, header output byte for byte.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`.

## Notes

## Log

- 2026-09-27: Created.
