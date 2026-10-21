---
id: BL-2867
title: Make upstream cases 158 and 415 pass: write each --include header line to the output before a reply that ends in error
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2867 — Make upstream cases 158 and 415 pass: write each --include header line to the output before a reply that ends in error

## Goal

Upstream cases 158 and 415 pass and are on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or the reason curl itself diverges is recorded in an ADR.

## Context

Split from BL-2831. The harness runs both with `--include` (as runtests.pl does) and compares the `--output` file with `<reply><data>` / `<datacheck>`. Measured 2026-10-19 on Windows: Curl's output file is empty, where the cases expect the header lines received before the failure:

- 158: `HTTP/1.1 100 Continue swsclose` + `Silly-header: yeeeees` + blank line, then the server closes; expected exit 52.
- 415: `HTTP/1.1 200 OK swsclose` + `Date: ...`, then `Content-Length: \r-6` fails; expected exit 8 (datacheck holds only the first two lines).

Hypothesis: curl writes each header line to the output as it parses it (`Curl_client_write` per header), so the lines before the failing point reach the file; Curl buffers the header block and drops it on error. Measure real curl first (`Record-CurlExchange.ps1`, local curl 8.22.0 Schannel): its exit code and output bytes for both. The gap office measured both `match | reference-diverges`, so check the reference result before changing the product. Find where `--include` headers are written (HTTP library or the transfer writer in `Curl.Console`); add that project to `touches` per the lane rules if it is not HTTP.

## Acceptance criteria

- [x] Notes record real curl's exit code and output bytes for 158 and 415, and a verdict: (a) Curl wrong, fixed; (b) harness wrong, fixed; (c) cannot pass in a platform-neutral harness, named in an ADR "Decided by Claude under Stewart's delegation".
- [x] For (a) or (b), both cases pass with `$env:CURL_UPSTREAM_CASE='<n>'` and are on `PassingUpstreamCases.txt`.
- [x] A product fix has a focused unit test in the fixed library's `.UnitTests` project that fails without it (ADR-0482).
- [x] Build clean, fast tests green, the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Real curl 8.22.0 (Schannel, `Record-CurlExchange.ps1`, `-i`, 2026-10-19): 158's shape exits 52
  (`Empty reply from server`) with output `HTTP/1.1 100 Continue swsclose\r\nSilly-header: yeeeees\r\n\r\n`
  (57 bytes); 415's exits 8 with output `HTTP/1.1 200 OK swsclose\nDate: Tue, 09 Nov 2010 14:49:00 GMT\n`
  (61 bytes). Verdict (a): Curl wrong, fixed.
- Fix in `Curl.Protocol.Http.UnitLibrary`: the head reader's empty-reply failure carries the 1xx
  heads read before it (`HttpResponseHeadReader.EmptyReply`), and a failure among the header lines
  carries every line received, the held (possibly-folded) header included
  (`HttpResponseHeadBuilder.HeadBytesWithPendingHeader`), so the handler's existing
  `HeadBytesBeforeFailure` path writes them to the header output before failing.
  `HeadBytesBeforeFailure` became settable for that.
- Unit tests: `ExecuteAsync_ClosedAfterAnInformationalHeadWithHeaderOutput_WritesThatHead` and
  `ExecuteAsync_HeaderLineFailsAfterAHeldHeaderWithHeaderOutput_WritesTheLinesBeforeIt`
  (both write nothing without the fix: before it the output was empty for both cases).
- Platform: both cases are plain HTTP over loopback with no paths or OS text, so the ratchet rows
  are platform-neutral.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 8b026f95 05ea46f1 640c74e3 68ad5856.
- 2026-10-19: Doing -> Done. Upstream cases 158 and 415 pass: -i writes the head lines read before a reply fails
