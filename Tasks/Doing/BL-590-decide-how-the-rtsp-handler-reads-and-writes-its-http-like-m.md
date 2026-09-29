---
id: BL-590
title: Decide how the RTSP handler reads and writes its HTTP-like messages
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-498, BL-515]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-590 — Decide how the RTSP handler reads and writes its HTTP-like messages

## Goal

An ADR fixes how `Curl.Protocol.Rtsp.UnitLibrary` writes RTSP/1.0 requests and reads replies without referencing `Curl.Protocol.Http`, which requests the curl 8.21.0 tool can make (by default `OPTIONS`; what `-X`, `-I`, `-d` and `-H` do on an `rtsp://` URL), and how CSeq and Session are tracked across requests on one connection.

## Context

- Conformance audit 2026-09-28, row 38 (Major, M-L; exits 85 `CURLE_RTSP_CSEQ_ERROR` and 86 `CURLE_RTSP_SESSION_ERROR` never produced). `Curl.Protocol.Rtsp.UnitLibrary/CLAUDE.md`: Abstractions only, `IConnection`.
- BL-579 decides the same question for WebSocket; if it lands first, follow it unless RTSP's needs differ, and say why.
- Measure first with `Record-CurlExchange.ps1` (the HTTP mode reads to `CRLF CRLF`, which RTSP requests end with): `curl rtsp://127.0.0.1:<P>/media`, with `-X DESCRIBE`, `-I`, `-H`, `-v`, and replies with a wrong `CSeq` and a wrong `Session`; record request bytes, stdout, stderr, exit code.
- RFC 2326. Depends on BL-498 (timeouts) and BL-515 (endpoints).

## Acceptance criteria

- [ ] The measurements are recorded in the ADR's Context.
- [ ] `Documentation/Planning/Decisions/ADR-<next free number>-<slug>.md` exists (number checked unused), Status Accepted, marked "Decided by Claude under Stewart's delegation", stating the request and reply code's home, the requests the tool makes and their options, and CSeq/Session tracking.
- [ ] Consequences list BL-591 to BL-593.
- [ ] `Documentation/Planning/Decisions/README.md` indexes the new ADR.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
