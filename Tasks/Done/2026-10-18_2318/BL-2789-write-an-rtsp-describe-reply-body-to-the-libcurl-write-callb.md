---
id: BL-2789
title: Write an RTSP DESCRIBE reply body to the libcurl write callback so upstream case 3100's stdout matches
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2789 — Write an RTSP DESCRIBE reply body to the libcurl write callback so upstream case 3100's stdout matches

## Goal

A libcurl RTSP `DESCRIBE` (any request kind libcurl delivers a reply body for) writes the reply body to the transfer's body output (the libcurl write callback), as curl 8.22.0's `lib/rtsp.c` does, while the curl tool's `OPTIONS *` keeps discarding it (ADR-0169).

## Context

Found by BL-2788. With BL-2788's 401 retry in place (stashed with BL-2788 if not yet committed), `CURL_UPSTREAM_CASE=3100` sends both `DESCRIBE`s as upstream expects, and the case then fails at `the program's stdout against <reply><data> differs at byte 273 (line 13): expected "v=0\r\n", got the end`: the 200 reply's 80-byte SDP body never reaches stdout. `Curl.Protocol.Rtsp.UnitLibrary/RtspProtocolHandler.cs` `SendAndReadAsync` calls `RtspReplyReader.DiscardBodyAsync`, which reports the body to `-v` only and never writes `context.Output`. Check upstream 567-572 and 689 in the same pass, since they may have bodies too.

## Acceptance criteria

- [x] A unit test in `Curl.Protocol.Rtsp.UnitTests` shows a libcurl `DESCRIBE` reply body written to `TransferContext.Output`, and the curl tool's `OPTIONS *` reply body still not written.
- [x] `Curl.Protocol.Rtsp.UnitLibrary` stays at 100% line and branch coverage with no method past the complexity gate.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Decision (matches curl 8.22.0 `lib/rtsp.c` `rtsp_do`): `DESCRIBE` and `GET_PARAMETER` clear `no_body`, so a libcurl run of either writes its reply body to the body output (`RtspRequestPlan.WritesReplyBody`); every other kind, and the tool's `OPTIONS *`, keeps reading and discarding it (ADR-0169). A `GET_PARAMETER` with no body still leaves the reply unread (heartbeat). No new ADR: this follows upstream's code directly.
- Upstream 567-572 and 689 checked: only 572 (a `GET_PARAMETER` with a body) also gets its reply body written now; the rest are `OPTIONS`, `SETUP`, `PLAY`, `ANNOUNCE`, `TEARDOWN`.
- `RtspReplyReader.DiscardBodyAsync` became `ReadBodyAsync` with an optional body output; a failed body write fails with 23. The body read moved out of `SendAndReadAsync` to keep that method at complexity 10.
- Case 3100 also needs BL-2788's 401 retry (still in Backlog), so it was not run here.
- Measured: Rtsp library at 100% line and 100% branch, worst CRAP 10, 0 failing members. Full fast run green.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 960d1c67 7ff73048 3d836d5e.
- 2026-10-18: Doing -> Done. A libcurl RTSP DESCRIBE or GET_PARAMETER writes its reply body to the write callback; the tool's OPTIONS * still discards it
