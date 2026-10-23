---
id: BL-3263
title: Clear ConcatenatedReadStream's deferred segment failure when the stream seeks
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Core.UnitLibrary/Multipart/ConcatenatedReadStream.cs, Curl.Core.UnitTests/Multipart]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3263 — Clear ConcatenatedReadStream's deferred segment failure when the stream seeks

## Goal

A multipart body that seeks back to its start, for example to resend after a 307 or 308, reads its bytes again instead of throwing a segment failure left over from before the seek.

## Context

The shift-end review (ADR-0710) found this in the merge `4d8ca7af8...9f0bbb58c`. The type-design-analyzer reported it, and the silent-failure-hunter reported the same thing.

- BL-3214 made every read fill its buffer across segments. When a segment throws after the fill already has bytes, the fill stores the exception in `deferredFailure` and returns those bytes:
  - `Curl.Core.UnitLibrary/Multipart/ConcatenatedReadStream.cs:200` (`FillOnce`)
  - `Curl.Core.UnitLibrary/Multipart/ConcatenatedReadStream.cs:257` (the async twin)
- The next read throws it through `ThrowDeferredFailure` (line 181).
- Nothing ever clears the field, so the problem has two parts:
  - `MoveTo` (line 301), behind `Position` and `Seek`, resets `current` and `segmentReads` but leaves `deferredFailure` set. The first read after a seek therefore throws the old failure.
  - `throw deferredFailure;` loses the original stack trace and throws the same instance on every later read.

Failure scenario: a `curl_mime_data_cb` read callback, or a file segment, fails after the earlier parts filled part of a buffer. `RedirectFollower` then seeks the body to 0 to resend it after a 307. The resent upload fails at once with the stale exception, but libcurl's rewind would resend it.

## Acceptance criteria

- [x] `MoveTo` clears `deferredFailure`, so a read after `Seek` or `Position = 0` returns the stream's bytes from the new position.
- [x] The deferred failure is rethrown with `ExceptionDispatchInfo.Capture(...).Throw()` (or an equivalent) so it keeps its original stack trace, and it is thrown once, not on every later read.
- [x] A new test in `Curl.Core.UnitTests/Multipart` (its own file) defers a failure behind a partial fill, seeks to 0, and asserts that the next read returns the first segment's bytes. The test fails without the fix.
- [x] `Curl.Core.UnitLibrary` stays at 100% line and branch coverage. `dotnet build` and the fast tests pass.

## Notes

- Quality run (Curl.Core.UnitLibrary): 0 failing members. The deferred failure is now cleared when thrown (once) and in MoveTo.

## Log

- 2026-10-23: Created by the shift-end review of 9f0bbb58c.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 6b5f6d6a 4daaf31a df065aa6 2ca7f145.
- 2026-10-23: Doing -> Done. ConcatenatedReadStream drops its deferred segment failure on seek and throws it once with its stack
