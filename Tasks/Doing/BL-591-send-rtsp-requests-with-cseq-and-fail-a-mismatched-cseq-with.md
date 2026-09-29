---
id: BL-591
title: Send RTSP requests with CSeq and fail a mismatched CSeq with exit 85
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-590]
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-591 — Send RTSP requests with CSeq and fail a mismatched CSeq with exit 85

## Goal

An `RtspProtocolHandler` sends the requests BL-590's ADR says the curl tool sends, byte for byte (request line, `CSeq`, `User-Agent`, header order), reads each reply head and body, writes the output curl writes, and fails a reply whose `CSeq` does not match with exit 85 and curl's message.

## Context

- Conformance audit 2026-09-28, row 38. Design and measurements: BL-590's ADR.
- Add any case BL-590 did not measure: a reply with no `CSeq`, a reply with a body (`Content-Length`), a `404`, with `Record-CurlExchange.ps1`.

## Acceptance criteria

- [ ] Any extra measurement copied into Notes.
- [ ] `Curl.Protocol.Rtsp.UnitTests` pin the request bytes and the output and outcome for each measured reply through a fake connection.
- [ ] No test needs `TestCategory=Integration`; tests are platform-neutral.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
