---
id: BL-943
title: Amend ADR-0172 section 7 now that HTTP/3 DATA frames stream
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-838]
touches: [Documentation/Planning/Decisions/ADR-0172-http-3-requests-run-on-an-http3session-and-fall-back-to-tcp-only-when-quic-fails.md]
requirement: none
created: 2026-09-29
completed:
---
# BL-943 — Amend ADR-0172 section 7 now that HTTP/3 DATA frames stream

## Goal

ADR-0172 section 7 ("Frame size") says what the code does after BL-838: `DATA` of any length streams, and only a frame of another type over 16 MiB fails with `ERR_H3_EXCESSIVE_LOAD`.

## Context

- Section 7 of `Documentation/Planning/Decisions/ADR-0172-http-3-requests-run-on-an-http3session-and-fall-back-to-tcp-only-when-quic-fails.md` still says `Curl.Http3` reads each frame's payload whole and a request stream fails any frame over 16 MiB, "Streaming `DATA` ... is its own task". BL-838 was that task.
- Now: `Http3FrameReader.ReadFrameOrDataAsync` (`Curl.Http3.UnitLibrary`) writes `DATA` payload bytes into the caller's buffer piece by piece; `Http3StreamConnection` (`Curl.Protocol.Http.UnitLibrary`) reads through it with a 16 KiB `DataBufferLength` buffer, and `MaximumFramePayloadLength` (16 MiB) applies only to `HEADERS` and other non-`DATA` frames.
- BL-838 could not edit the ADR: BL-911 held `Documentation/Planning/Decisions` at the time.

## Acceptance criteria

- [ ] ADR-0172 section 7 states that `DATA` frames of any length stream through a 16 KiB buffer (BL-838) and that only a non-`DATA` frame over 16 MiB fails with exit 56 and `ERR_H3_EXCESSIVE_LOAD`; the sentence calling streaming "its own task" is gone.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
