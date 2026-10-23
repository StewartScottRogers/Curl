---
id: BL-3283
title: Write curl's Unsolicited Transfer-Encoding verbose line (differential srv-te-gzip-chunked-v)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3283 — Write curl's Unsolicited Transfer-Encoding verbose line (differential srv-te-gzip-chunked-v)

## Goal

curl -sv --compressed against a response with Transfer-Encoding: gzip, chunked writes "* Unsolicited Transfer-Encoding (gzip) found" before closing, as curl 8.22.0 does.

## Context

BL-3233 differential case srv-te-gzip-chunked-v (-sv --compressed {U}/te-gzip-chunked): curl 8.22.0 writes "* Unsolicited Transfer-Encoding (gzip) found" after the status line and before "* closing connection #0"; Curl omits it. Check exit code and body too.

## Acceptance criteria

- [x] Curl's stderr for srv-te-gzip-chunked-v matches curl 8.22.0's, pinned by a unit test.
- [x] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for each changed library.

## Notes

- Measured curl 8.22.0 (ucrt64, Record-CurlExchange.ps1) with -sv: `--compressed` against `Transfer-Encoding: gzip, chunked` exits 61 and writes `< HTTP/1.1 200 OK` then `* Unsolicited Transfer-Encoding (gzip) found`, no Transfer-Encoding line and no empty line; `chunked, identity` (no options) and `chunked, gzip` with `--tr-encoding` write their exit 61 messages the same way. Curl already failed with the right exit code and message but wrote no `*` line.
- Fix: HttpTransferEncoding.Rejected gives every exit 61 Transfer-Encoding refusal its message as an info line (curl's failf writes it under -v). Pinned by HttpProtocolHandlerTests.RefusedTransferEncodingVerbose.cs for the three measured responses. Not measured: the too-many-codings refusal, which shares the path and so now writes its line too, as failf would.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary: 0 failing members. Solution build clean, fast tests green (Windows); the change and its test are platform-neutral.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: a8d8ea8f 69edf767.
- 2026-10-23: Doing -> Done. curl -sv writes the exit 61 Transfer-Encoding refusal (Unsolicited Transfer-Encoding (gzip) found and the others) as a * line, as curl 8.22.0 does
