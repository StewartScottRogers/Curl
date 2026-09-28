---
id: BL-480
title: Stop reading a head at a refused header while the peer holds it open
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-479]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Record-CurlExchange.ps1]
requirement: none
created: 2026-09-27
completed:
---
# BL-480 — Stop reading a head at a refused header while the peer holds it open

## Goal

When a final response head carries a header curl 8.21.0 refuses and the peer then sends nothing more without closing, `HttpProtocolHandler` fails at once with the refused header's exit code and message, as curl 8.21.0 does, instead of waiting for the head to end or for `-m` to expire.

## Context

- Since BL-475 and BL-479, `HttpResponseHeadReader` asks `FindRefusal` of a final head's whole headers only when the head ends (empty line or peer close) or fails. A peer that holds the connection open after a refused header leaves the reader waiting in `HttpLineReader.ReadLineAsync`.
- Checking each header as it arrives was rejected in BL-475 as O(n²) (BL-475 Notes). A route that keeps the cost bounded: ask `FindRefusal` of the whole headers held only when the line reader has no whole line buffered and must read from the connection.
- Measure first: `Record-CurlExchange.ps1` sends the response and closes, so extend it (for example a `-HoldOpenMilliseconds` parameter) to keep the connection open after the response, then run `-s -v http://127.0.0.1:<port>/` against `HTTP/1.1 200 OK`, `X-Before: 1`, `Content-Length: x` with no empty line, and time how long curl takes to exit.

## Acceptance criteria

- [ ] A test over a scripted connection that never ends the head after `Content-Length: x` pins exit 8, `Invalid Content-Length: value` and the `-v` head lines before it, as measured, without the connection closing.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http.UnitLibrary`.

## Notes

- Filed from BL-479 (2026-09-27).

## Log

- 2026-09-27: Created.
