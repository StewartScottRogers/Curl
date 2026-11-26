---
id: BL-5966
title: Keep curl's exit 0 when the GOAWAY for DATA past a -I HTTP/2 response cannot be sent
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitLibrary/Http2Session.cs, Curl.Original.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5966 — Keep curl's exit 0 when the GOAWAY for DATA past a -I HTTP/2 response cannot be sent

## Goal

When a `-I` HTTP/2 response carries DATA past its promised length and the GOAWAY PROTOCOL_ERROR that follows cannot be written because the peer has already gone, Curl still exits 0 as curl does.

## Context

- Found by the shift-end review of 2026-11-24 (pr-review-toolkit silent-failure-hunter), commit e88562fb18 (BL-5938, ADR-1384).
- `Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:693`, in `EndAtUnwantedBodyAsync`: `session.Frames.SendGoAwayAsync(Http2ErrorCode.ProtocolError, ...)` is unguarded. `Http2Connection.SendGoAwayAsync` (`Curl.Http2.UnitLibrary/Http2Connection.cs:389`) does not catch, so an `IOException` from a server that sent HEADERS and DATA then closed escapes through `TakeAsync` and ends the transfer as a send or receive error, where curl exits 0 (measured, curl 8.18.0, BL-5938 Notes).
- Every other GOAWAY send in the HTTP/2 code treats an unsendable GOAWAY as "the transport is already gone": `Http2Session.cs:194`, `:521`, `:549`. `ShutDownMalformedAsync` (line 731) has the same gap but predates this diff; fix it too if the same guard applies.

## Acceptance criteria

- [x] The GOAWAY send in `EndAtUnwantedBodyAsync` catches `IOException` the way `Http2Session.ShutDownAsync` does, and the `-v` lines it writes stay as they are.
- [x] A unit test in `Curl.Original.Protocol.Http.UnitTests` gives a `-I` HTTP/2 response DATA past its length on a connection whose writes throw `IOException`, and asserts exit 0.
- [x] `dotnet build` is clean and the fast tests of `Curl.Original.Protocol.Http.UnitTests` pass; the library keeps 100% line and branch coverage.

## Notes

- `Http2StreamConnection.SendProtocolErrorGoAwayAsync` now guards both GOAWAY PROTOCOL_ERROR sends: `EndAtUnwantedBodyAsync` and `ShutDownMalformedAsync`. For the latter the transfer still reports the malformed response (exit 16) instead of a send error.
- Catching alone changed the `-v` lines: `IsGoAwaySent` stayed false, so the session still accepted new streams and `-v` ended `left intact`. The catch therefore calls the new `Http2Session.MarkPeerGone`, and `AcceptsNewStreams` is false after it, so the connection ends `shutting down connection #0` as before and is not reused. `touches` widened to `Http2Session.cs` for this; no task in Doing on origin/work/dark-factory named it.
- Tests: `Http2HeadResponseWithDataTests` (two new) over the new `Fakes/GoAwayRefusingConnection`, whose GOAWAY writes throw `IOException`. All other writes succeed, so the request itself goes out.
- Gates on 2026-11-25: `dotnet build` clean, all fast tests green (HTTP unit tests 2844 passed, 44 skipped), `Measure-CodeQuality.ps1 -Library Curl.Original.Protocol.Http.UnitLibrary` 0 failing members.

## Log

- 2026-11-25: Created by the shift-end review (ADR-0710).
- 2026-11-25: Backlog -> Doing. Criteria at claim: c679fb5c ec314102 5e699e0a.
- 2026-11-25: Doing -> Done. curl -I over HTTP/2 exits 0 and shuts the connection down when the GOAWAY for extra DATA cannot be sent
