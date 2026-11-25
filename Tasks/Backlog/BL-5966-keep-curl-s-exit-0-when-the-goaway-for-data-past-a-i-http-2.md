---
id: BL-5966
title: Keep curl's exit 0 when the GOAWAY for DATA past a -I HTTP/2 response cannot be sent
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Original.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-25
completed:
---
# BL-5966 — Keep curl's exit 0 when the GOAWAY for DATA past a -I HTTP/2 response cannot be sent

## Goal

When a `-I` HTTP/2 response carries DATA past its promised length and the GOAWAY PROTOCOL_ERROR that follows cannot be written because the peer has already gone, Curl still exits 0 as curl does.

## Context

- Found by the shift-end review of 2026-11-24 (pr-review-toolkit silent-failure-hunter), commit e88562fb18 (BL-5938, ADR-1384).
- `Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:693`, in `EndAtUnwantedBodyAsync`: `session.Frames.SendGoAwayAsync(Http2ErrorCode.ProtocolError, ...)` is unguarded. `Http2Connection.SendGoAwayAsync` (`Curl.Http2.UnitLibrary/Http2Connection.cs:389`) does not catch, so an `IOException` from a server that sent HEADERS and DATA then closed escapes through `TakeAsync` and ends the transfer as a send or receive error, where curl exits 0 (measured, curl 8.18.0, BL-5938 Notes).
- Every other GOAWAY send in the HTTP/2 code treats an unsendable GOAWAY as "the transport is already gone": `Http2Session.cs:194`, `:521`, `:549`. `ShutDownMalformedAsync` (line 731) has the same gap but predates this diff; fix it too if the same guard applies.

## Acceptance criteria

- [ ] The GOAWAY send in `EndAtUnwantedBodyAsync` catches `IOException` the way `Http2Session.ShutDownAsync` does, and the `-v` lines it writes stay as they are.
- [ ] A unit test in `Curl.Original.Protocol.Http.UnitTests` gives a `-I` HTTP/2 response DATA past its length on a connection whose writes throw `IOException`, and asserts exit 0.
- [ ] `dotnet build` is clean and the fast tests of `Curl.Original.Protocol.Http.UnitTests` pass; the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-11-25: Created by the shift-end review (ADR-0710).
