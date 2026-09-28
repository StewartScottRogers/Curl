---
id: BL-483
title: Accept a head the peer closes right after a refused header, as curl 8.21.0 does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-483 — Accept a head the peer closes right after a refused header, as curl 8.21.0 does

## Goal

When the peer closes the connection right after the line of a header curl 8.21.0 would refuse, with no byte of a next line (or only a blank) before the close, `HttpProtocolHandler` does what curl 8.21.0 does - measured: exit 0, the refused header's `-v` line reported, `* Connection #0 to host ... left intact` - instead of failing with the refused header's error.

## Context

- curl 8.21.0 does not act on a header until a byte of the next line shows it is whole (a blank would fold the next line into it), so a header the head ends on at close is never checked (BL-480 Notes).
- Since BL-475/BL-479, `HttpResponseHeadReader.ReadAsync` asks `FindRefusal` of the whole head when the peer closes, the last header included, so this case fails with exit 8 (`Invalid Content-Length: value`).
- Measured (2026-09-27, `Record-CurlExchange.ps1`, `-s -v http://127.0.0.1:<port>/`) against `HTTP/1.1 200 OK\r\nX-Before: 1\r\nContent-Length: x\r\n` then close: exit 0, stderr `< HTTP/1.1 200 OK`, `< X-Before: 1`, `< Content-Length: x`, `* Connection #0 to host 127.0.0.1:<port> left intact`. Measure again with `-sS -D -`, `-w '%{num_headers}'` and a `Transfer-Encoding: bogus` last header before pinning.
- A route: at close, exclude the last header from `FindRefusal` unless a byte after it arrived.

## Acceptance criteria

- [ ] A test over a scripted connection that closes right after `Content-Length: x\r\n` pins the measured exit code, `-v` events and `-D` output.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-480 (2026-09-27).

## Log

- 2026-09-27: Created.
