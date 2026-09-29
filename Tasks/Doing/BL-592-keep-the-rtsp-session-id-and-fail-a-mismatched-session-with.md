---
id: BL-592
title: Keep the RTSP session ID and fail a mismatched session with exit 86
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-591]
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-592 — Keep the RTSP session ID and fail a mismatched session with exit 86

## Goal

The RTSP handler records the `Session` header from a reply, sends it on later requests of the same transfer as curl 8.21.0 does, and fails a reply whose session does not match with exit 86 and curl's message.

## Context

- Conformance audit 2026-09-28, row 38. Builds on BL-591; which tool invocations reach a second request (and so can see a session) is in BL-590's ADR.
- Measure with `Record-CurlExchange.ps1 -Connections 1` and several responses: a `SETUP`-style reply carrying `Session: 1234;timeout=60` followed by a reply with `Session: 9999`, if the tool can make that sequence; if it cannot, record that in Notes and pin the handler behaviour from RFC 2326 section 12.37 with the exit code only.

## Acceptance criteria

- [ ] Measured first as above (or the impossibility recorded); request bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Rtsp.UnitTests` pin the session header on later requests and exit 86 for a mismatch.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
