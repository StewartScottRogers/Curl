---
id: BL-5426
title: Skip the HTTP/2 body length check when -I ends the stream at the final head
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5426 — Skip the HTTP/2 body length check when -I ends the stream at the final head

## Goal

`curl -I -X GET --http2` against a server answering with a `Content-Length` ends at the head with exit 0, as curl does, instead of exit 16.

## Context

Found by the shift-end review (ADR-0710) of the 2026-11-17 shift (code-reviewer agent), in BL-5391's change.
`Curl.Original.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:547` now ends the response at the final head when `EndsAtFinalHead` is set (from `ignoresBody`, `Http2Session.cs:165`). `PromisedBodyLengthOf` (`:595-596`) gives 0 only when `:method` is HEAD (or the status is 204 or 304), so with `-I -X GET` the promised length is the `Content-Length`. The check at `:634` (`bodyBytesReceived != promisedBodyLength`) then sends GOAWAY PROTOCOL_ERROR and fails with exit 16.

## Acceptance criteria

- [x] Ending the stream because of `EndsAtFinalHead` skips the promised-length check, or `PromisedBodyLengthOf` treats `EndsAtFinalHead` like a HEAD.
- [x] A unit test in `Curl.Original.Protocol.Http.UnitTests`: an HTTP/2 GET stream with `EndsAtFinalHead` and a final head with `content-length: 5` ends successfully with no GOAWAY.
- [x] BL-5391's tests still pass; quality gates hold.

## Notes

Fix: `EndResponseAsync` skips the promised-length check when `EndsAtFinalHead` is set (the body is not read then). Test `ReadToEndAsync_GetStreamEndingAtFinalHeadWithContentLength_EndsWithoutGoAway`. Quality run on the library: 0 failing members. CI on Linux and macOS: change is platform-neutral.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 8563fedd a0d25e08 39a0bcb6.
- 2026-11-18: Doing -> Done. HTTP/2 -I ends at the final head without the length check; test added
