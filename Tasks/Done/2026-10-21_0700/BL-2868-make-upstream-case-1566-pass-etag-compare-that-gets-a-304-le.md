---
id: BL-2868
title: Make upstream case 1566 pass: --etag-compare that gets a 304 leaves the -o file untouched
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2868 — Make upstream case 1566 pass: --etag-compare that gets a 304 leaves the -o file untouched

## Goal

Upstream case 1566 passes and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or the reason curl itself diverges is recorded in an ADR.

## Context

Split from BL-2831. test1566 runs `-o %LOGDIR/output1566 --etag-compare %LOGDIR/etag1566` (option `no-output,no-include`) with the output file already holding `downloaded already`; the server answers `304 Not modified` with no body. Measured 2026-10-19 on Windows: the output file differs at byte 0 - Curl truncates it. curl opens the `-o` file lazily, on the first body byte, so a bodiless 304 leaves it untouched.

Measure real curl first (`Record-CurlExchange.ps1`, local curl 8.22.0 Schannel) with a pre-existing output file. Find where `-o` opens its file (`Curl.Console` or the CLI's output writer, `grep -rn "FileMode.Create"`); add that project to `touches` per the lane rules if it is not `Curl.Console`. Check what a lazy open does to `--create-dirs`, `--remove-on-error`, `--no-clobber` and an empty 200 body (curl creates an empty file for a 200 with no body at transfer end) before changing it.

## Acceptance criteria

- [x] Notes record real curl's exit code and file contents for 1566, and a verdict (a), (b) or (c) as BL-2831 defines them.
- [x] For (a) or (b), 1566 passes with `$env:CURL_UPSTREAM_CASE='1566'` and is on `PassingUpstreamCases.txt`.
- [x] A product fix has a focused unit test in the fixed library's `.UnitTests` project that fails without it (ADR-0482).
- [x] Build clean, fast tests green, the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

- Measured 2026-10-19 with `Record-CurlExchange.ps1`, curl 8.22.0 Schannel: `-o out --etag-compare etag` (`If-None-Match: "123456"`) answered `304 Not modified` with no body exits 0 and leaves the pre-existing `out` holding `downloaded already\n` byte for byte. A plain 304 with no condition at all also leaves an existing file untouched, and creates no file when none exists; a bodiless 204 does create the empty file. So curl skips its end-of-transfer empty-file creation for every 304, not only for an unmet `-z`.
- Verdict (a): Curl was wrong. Curl does open `-o` lazily already; the truncation came from `DeferredOutputFileStream.CompleteAsync`, which created the file empty after any successful bodiless transfer unless `TimeConditionUnmet`. It now also skips that when the result's report says 304 (`CreatesEmptyFileAfter`, split out to keep `CompleteAsync` within complexity 10). `--create-dirs`, `--no-clobber` and `--remove-on-error` are unaffected: the open itself is unchanged, only whether the empty file is made after a 304. A 200 or 204 with no body still creates the empty file.
- Focused tests: `DeferredOutputFileStreamTests.CompleteAsync_NothingWrittenAfterA304_LeavesTheFileUnopened` (fails without the fix) and `CompleteAsync_NothingWrittenAfterA204_CreatesTheEmptyFile`. Case 1566 passes with `CURL_UPSTREAM_CASE=1566` and is on the ratchet; it compares file contents only, so it is platform-neutral.
- `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line, 100% branch, 0 failing members, worst CRAP 10. Build clean; fast tests green (Curl.Console.UnitTests 2829 passed, Curl.Conformance.UnitTests 3597 passed).

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 46062614 aa522d3d 640c74e3 68ad5856.
- 2026-10-19: Doing -> Done. Upstream case 1566 passes: a 304 no longer creates or truncates the -o file
