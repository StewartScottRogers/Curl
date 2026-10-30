---
id: BL-4295
title: WebSocketExtendedConnect: open and run a WebSocket over HTTP/2 (RFC 8441)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4094, BL-4196, BL-4194, BL-4195, BL-4298]
touches: [Curl.Extension.WebSocketExtendedConnect.UnitLibrary/Http2, Curl.Extension.WebSocketExtendedConnect.UnitTests/Http2]
requirement: none
created: 2026-10-29
completed:
---
# BL-4295 — WebSocketExtendedConnect: open and run a WebSocket over HTTP/2 (RFC 8441)

## Goal

A client flow in the extension opens a WebSocket on an HTTP/2 stream with extended CONNECT and exchanges frames over it.

## Context

Second part of BL-4112. Uses the wire layer from BL-4298, `Http2Connection` in the shared `Curl.Http2.UnitLibrary`, the shared WebSocket codec from BL-4196 and the scheme hook from BL-4195. Specification: RFC 8441.

## Acceptance criteria

- [ ] A client flow in `Http2/` waits for the server's SETTINGS_ENABLE_CONNECT_PROTOCOL, sends the extended CONNECT on a new stream and treats a 2xx response as the open WebSocket (RFC 8441 sections 3 and 4); unit tests with fake connections or recorded bytes, no network.
- [ ] Data frames on that stream carry WebSocket frames both ways, and END_STREAM or RST_STREAM closes the connection as RFC 8441 section 5 says; unit tests cite the sections.
- [ ] A server that does not advertise the setting, or answers non-2xx, fails with a clear error; unit tests.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- Split from BL-4112 (the dark factory's cost cap).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: a2f1a684 d6cf6ff8 db287935 e35aff77 3d836d5e.
