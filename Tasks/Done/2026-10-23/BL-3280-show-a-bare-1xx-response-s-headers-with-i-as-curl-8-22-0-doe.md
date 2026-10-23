---
id: BL-3280
title: Show a bare 1xx response's headers with -i, as curl 8.22.0 does (differential status-100)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3280 — Show a bare 1xx response's headers with -i, as curl 8.22.0 does (differential status-100)

## Goal

curl -si on a URL answering only HTTP/1.1 100 with Content-Length writes the 100 header block to stdout, as curl 8.22.0 does.

## Context

BL-3233 differential case status-100 (-si {U}/status/100, route in Curl.Conformance.Differential.UnitLibrary/Http): real curl 8.22.0 writes "HTTP/1.1 100 Status\r\nDate: ...\r\nContent-Type: text/plain\r\nContent-Length: 11\r\n\r\n", Curl writes nothing. Measure real curl before pinning.

## Acceptance criteria

- [x] Curl's stdout for the status-100 case matches curl 8.22.0's, pinned by a unit test in Curl.Protocol.Http.UnitTests.
- [x] The fast tests pass and Measure-CodeQuality.ps1 reports 0 failing members for each changed library.

## Notes

- 2026-10-23 lane-7: measured curl 8.22.0 (Git ucrt64, Schannel) with Record-CurlExchange.ps1 -Response 'HTTP/1.1 100 Status\r\nContent-Type: text/plain\r\nContent-Length: 11\r\n\r\nstatus 100\n' -HoldOpenMilliseconds 3000 -CurlArgs -si,-v: exit 8, stdout is the 100 head through its empty line, -v says "Invalid status line" then "Invalid response header". Curl already failed with exit 8 but dropped the head: `HttpResponseHeadReader.ReadStatusLineAfterAsync` now carries the 1xx heads read so far as `HeadBytesBeforeFailure`, which the handler writes to the header output, as it already did for test158's closed-after-1xx reply. Pinned by `ExecuteAsync_BodyAfterAnInformationalHeadWithHeaderOutput_WritesThatHead`. Pipeline run direct: a one-line fix needing no plan; no ADR, the behaviour is real curl's, measured.
- Fast tests green (solution, Windows; the test is platform-neutral). Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary: 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: a7bb6b68 69edf767.
- 2026-10-23: Doing -> Done. curl -si on a bare 1xx answer with a body writes the 1xx head before failing with exit 8, as curl 8.22.0 does
