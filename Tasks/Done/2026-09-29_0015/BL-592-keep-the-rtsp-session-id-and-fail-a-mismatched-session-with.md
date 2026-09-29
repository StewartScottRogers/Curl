---
id: BL-592
title: Keep the RTSP session ID and fail a mismatched session with exit 86
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-591]
touches: [Curl.Protocol.Rtsp.UnitLibrary, Curl.Protocol.Rtsp.UnitTests, Documentation/Planning/Decisions/ADR-0169-the-rtsp-library-writes-its-own-options-request-and-reads-its-own-reply-with-cseq-checked-per-transfer.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-592 — Keep the RTSP session ID and fail a mismatched session with exit 86

## Goal

The RTSP handler records the `Session` header from a reply, sends it on later requests of the same transfer as curl 8.21.0 does, and fails a reply whose session does not match with exit 86 and curl's message.

## Context

- Conformance audit 2026-09-28, row 38. Builds on BL-591; which tool invocations reach a second request (and so can see a session) is in BL-590's ADR.
- Measure with `Record-CurlExchange.ps1 -Connections 1` and several responses: a `SETUP`-style reply carrying `Session: 1234;timeout=60` followed by a reply with `Session: 9999`, if the tool can make that sequence; if it cannot, record that in Notes and pin the handler behaviour from RFC 2326 section 12.37 with the exit code only.

## Acceptance criteria

- [x] Measured first as above (or the impossibility recorded); request bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Rtsp.UnitTests` pin the session header on later requests and exit 86 for a mismatch.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- The tool cannot make a second request in one transfer (ADR-0169 rows 1-3, 20), so the
  `SETUP`-then-mismatch sequence of the Context is impossible from the command line. Exit 86 is
  still reachable: one reply carrying two `Session` headers. Measured on curl 8.21.0 (Git for
  Windows mingw64, Schannel) with `Record-CurlExchange.ps1`, `-sS -i rtsp://127.0.0.1:<port>/media`
  unless named. mingw curl writes stderr in text mode, so each `\n` there shows as `\r\n`; the
  message below is the logical one (`[9999\r\n]` arrived as `[9999\r\r\n]`).

  | Reply after `RTSP/1.0 200 OK\r\nCSeq: 1\r\n` / arguments | stdout | stderr | Exit |
  | --- | --- | --- | --- |
  | `Session: 1234;timeout=60\r\nSession: 9999\r\n\r\n` | head up to the first `Session` line | `curl: (86) Got RTSP Session ID Line [9999\r\n], but wanted ID [1234]` | 86 |
  | `Session: 1234;timeout=60\r\nSession: 1234\r\n\r\n` | whole head | | 0 |
  | `Session:\r\n\r\n`, `Session:  \r\n\r\n` | whole head | | 0 |
  | `Session: ;x\r\nSession: 5\r\n\r\n` | head up to `Session: ;x` | `…Line [5\r\n], but wanted ID []` | 86 |
  | `session: \tab c\r\nSESSION: ab\r\n\r\n` | whole head | | 0 |
  | `Session: 12\r\nSession: 123\r\n\r\n` | head up to `Session: 12` | `…Line [123\r\n], but wanted ID [12]` | 86 |
  | `Session:\t 7 ;x\r\nSession:7\r\n\r\n` | | | 0 |
  | `Session: a\xe9b\r\nSession: a\r\n\r\n` | | | 0 (the ID stops at a byte of 0x80 or more) |
  | `Session: a\x7fb\r\nSession: a\r\n\r\n` | | `…Line [a\r\n], but wanted ID [a\x7fb]` | 86 |
  | `RTSP/1.0 200 OK\r\nSession: 1\r\nSession: 2\r\nCSeq: 1\r\n\r\n` | head up to `Session: 1` | `…Line [2\r\n], but wanted ID [1]` (86 before the CSeq check) | 86 |
  | LF-only: `RTSP/1.0 200 OK\nCSeq: 1\nSession: 1\nSession: 2\n\n` | head up to `Session: 1\n` | `…Line [2\n], but wanted ID [1]` | 86 |
  | `-f`; `RTSP/1.0 404 Not Found\r\nCSeq: 1\r\nSession: 1\r\nSession: 2\r\n\r\n` | head up to `Session: 1` | `…Line [2\r\n], but wanted ID [1]` (86 ahead of 22) | 86 |
  | `-H 'Session: 5'`, `-H 'Session:'`, `-H 'session;'` | nothing sent | `curl: (43) Session ID cannot be set as a custom header.` | 43 |
  | `-H 'Session: 5' -H 'CSeq: 3'` | nothing sent | `curl: (85) CSeq cannot be set as a custom header.` | 85 |
  | `-H 'X-Session: 1'` | request `…User-Agent: curl/8.21.0\r\nX-Session: 1\r\n\r\n` | | 0 |

- Built: `RtspSessionState` (CSeq counter and kept session ID, per transfer, ADR-0169 decision 4),
  read by `RtspReplyHeadParser` for every `Session:` line (any case); the handler's
  `ExchangeAsync(connection, context, session)` makes one request with the next `CSeq` and the
  kept ID, and `ExecuteAsync` calls it once. `-H Session` refused with 43 after the `CSeq` check.
- Defaults taken (unmeasurable from the tool, which never sends a second request): a kept empty
  ID is sent as `Session: ` because libcurl's `rtsp.c` sends any ID it holds; libcurl's
  `Got a blank Session ID` branch (a `Session:` with nothing after it, not even a line ending) is
  unreachable here because every line the parser reads keeps its line ending, so it is not built.
- ADR-0169 said exit 86 could not be reached from the command line; corrected it with the
  measurement above (added to `touches`; no task in Doing names the file).
- Tests: 106 in `Curl.Protocol.Rtsp.UnitTests` (21 new, data rows included). Review done in
  session against ADR-0169 and the measurements.

- Rerun on lane 3 (2026-09-28): lane 4's commit `6b1599da` cherry-picked unchanged onto the
  current branch; `dotnet build Curl.slnx -warnaserror` clean, every fast-test project passes
  (Rtsp 106), and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Rtsp.UnitLibrary` reports
  100/100 with 0 failing members (worst CRAP 8). The earlier integration failure did not
  reproduce, so it came from another lane's work, not this change.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Backlog. Lane 4 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-592-lane-4-20260928-212155; start with git cherry-pick --no-commit factory/BL-592-lane-4-20260928-212155 and fix it.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. RTSP transfers keep the reply's Session ID, send it on later requests, fail a contradicting Session with exit 86 and a -H Session with 43, as curl 8.21.0 does
