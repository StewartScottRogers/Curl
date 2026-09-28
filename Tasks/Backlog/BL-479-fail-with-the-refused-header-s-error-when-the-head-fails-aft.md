---
id: BL-479
title: Fail with the refused header's error when the head fails after it
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-475]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-479 — Fail with the refused header's error when the head fails after it

## Goal

When a final response head carries a header curl 8.21.0 refuses (an invalid Content-Length, a refused Transfer-Encoding, too many content codings) and then fails later - a line with no colon, a line or head too large, or a peer that never sends the empty line - `HttpProtocolHandler` fails with the refused header's exit code and message, reporting only the head before it, as curl 8.21.0 does.

## Context

- Since BL-475, `HttpResponseHeadReader` holds a final head's whole headers until the head ends, then asks `FindRefusal` once of the whole head and releases only the headers before the refused one. A head that fails before it ends never reaches `FindRefusal`: the later failure wins, and `ReportHeldLines` releases every held header, refused one included.
- curl 8.21.0 stops reading the head at the refused header (BL-475 Notes), so it should never see the later failure, and should not wait for a peer that never ends the head.
- Measure first with `Record-CurlExchange.ps1`: `-s -v http://127.0.0.1:<port>/` against `HTTP/1.1 200 OK`, `Content-Length: x`, `no colon`, empty line; and against a head with `Content-Length: x` that the server never ends (close after a delay).
- A route: on a failure inside the final head, ask `FindRefusal` of the headers already whole (`HttpResponseHeadBuilder` has them) before reporting the failure.

## Acceptance criteria

- [ ] A test over a scripted connection pins the exit code, message and `-v` events for a head whose refused header is followed by a line with no colon, as measured.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-475 (2026-09-27).

## Log

- 2026-09-27: Created.
