---
id: BL-231
title: Register HttpProtocolHandler and fill HttpRequestOptions from the command line
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-173, BL-175, BL-187, BL-188, BL-230]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-231 — Register HttpProtocolHandler and fill HttpRequestOptions from the command line

## Goal

`curl http://...` and `curl https://...` run end to end in `Curl.Console`, with `-X`, `-H`, `-A`, `-e`, the data options, `-G` and `--json` giving curl's request bytes.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured: default GET `GET /a?b HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n`; `-d x=1` `POST / HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\nContent-Length: 3\r\nContent-Type: application/x-www-form-urlencoded\r\n\r\nx=1`.
- Composition is explicit in `Curl.Console/CurlComposition.cs` (native AOT; no reflection).

## Acceptance criteria

- [ ] Over a fake connector (`Curl.Console.UnitTests/ScriptedConnector.cs`), `curl http://...` and `curl https://...` send the measured bytes and write the body to stdout.
- [ ] `-X`, `-H`, `-A`, `-e`, `-d`, `--data-*`, `-G` and `--json` each have a test with measured bytes.
- [ ] `HttpProtocolHandler` is registered explicitly in `CurlComposition`.
- [ ] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Plan item: W2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
