---
id: BL-364
title: Reject a response listing more than five Content-Encoding codings under --compressed, as curl 8.21.0 does
priority: Low
assignee: Claude
pipeline: protocol
depends-on: [BL-315]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-364 — Reject a response listing more than five Content-Encoding codings under --compressed, as curl 8.21.0 does

## Goal

With `--compressed`, a response whose Content-Encoding headers list more than five codings fails with exit 61 `Reject response exceeding limit of 5 content encodings`, as curl 8.21.0 does.

## Context

- Found while measuring BL-315 (2026-09-27). curl 8.21.0 (mingw, `Record-CurlExchange.ps1 -Port 18180`) with `-sS --compressed` against `HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Encoding: identity, identity, identity, identity, identity, identity\r\n\r\nhello`: stdout empty, stderr `curl: (61) Reject response exceeding limit of 5 content encodings`, exit 61.
- Curl's `HttpContentDecoder` (Curl.Protocol.Http.UnitLibrary) has no limit today. BL-315 added the Transfer-Encoding equivalent in `HttpTransferEncoding.Requested` (limit 5, `identity` counted); the Content-Encoding limit is counted separately (a Transfer-Encoding gzip plus five Content-Encoding gzip was not rejected for the count, BL-315 Notes).
- Still to measure: five codings (expected accepted), whether an empty body with six is still refused (the limit is checked when the headers are read, not at the first body byte), and whether `--raw --compressed` skips it.

## Acceptance criteria

- [ ] The six-coding case above is pinned in a `Curl.Protocol.Http.UnitTests` test with 1-byte reads, and the cases still to measure are measured, recorded in Notes and pinned.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
