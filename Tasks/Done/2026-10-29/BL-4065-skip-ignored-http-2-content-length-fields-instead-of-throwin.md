---
id: BL-4065
title: Skip ignored HTTP/2 content-length fields instead of throwing FormatException
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4065 — Skip ignored HTTP/2 content-length fields instead of throwing FormatException

## Goal

An HTTP/2 response whose `content-length` value contains CR, LF or NUL ends with a curl exit code, not an unhandled `FormatException`.

## Context

Found by the shift-end review of 2026-10-28 (code-reviewer, ADR-0710), in the diff `354cb7711...c4b167258`.

- `Curl.Protocol.Http.UnitLibrary/Http2StreamConnection.cs:633-634`: `ContentLengthOf` takes the first `content-length` field and calls `long.Parse` on it. Its doc says the field was "already checked by `Http2ResponseHead.IndexOfInvalidField`".
- That check is not done for this field. `Http2ResponseHead.cs:60/77` (`IsIgnoredRegularField`) skips any regular field whose value holds CR, LF or NUL, so such a field is never validated.
- A server can send `content-length: 1\0` (HPACK allows it). `long.Parse` then throws `FormatException`, which no handler maps to an exit code.

## Acceptance criteria

- [x] `ContentLengthOf` skips the fields `Http2ResponseHead` ignores, or no longer throws on them.
- [x] Its behaviour on such a head matches what curl 8.22.0 with nghttp2 does, measured, or the reason it is left unmeasured is recorded in Notes.
- [x] A test in `Curl.Protocol.Http.UnitTests` sends a HEADERS frame with a NUL in `content-length`. It checks that the transfer ends with a `CurlExitCode`, not `FormatException`.
- [x] The doc comment on `ContentLengthOf` says what it does.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- ContentLengthOf now skips fields Http2ResponseHead.IsIgnored (made internal) ignores, so a content-length with CR, LF or NUL is treated as absent, as IndexOfInvalidField already treats it. Left unmeasured against curl 8.22.0: no loopback h2 server can send the NUL through nghttp2 here; the choice follows the existing IsIgnored measurement (BL-3667). With no length the transfer ends Ok (exit 0), which the new test pins.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 09313c28 7cc069be 2a4e6067 a0392a71 3d836d5e.
- 2026-10-29: Doing -> Done. HTTP/2 content-length with CR/LF/NUL is skipped instead of throwing FormatException
