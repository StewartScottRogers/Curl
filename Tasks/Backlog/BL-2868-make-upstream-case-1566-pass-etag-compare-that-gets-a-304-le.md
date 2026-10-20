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
completed:
---
# BL-2868 — Make upstream case 1566 pass: --etag-compare that gets a 304 leaves the -o file untouched

## Goal

Upstream case 1566 passes and is on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, or the reason curl itself diverges is recorded in an ADR.

## Context

Split from BL-2831. test1566 runs `-o %LOGDIR/output1566 --etag-compare %LOGDIR/etag1566` (option `no-output,no-include`) with the output file already holding `downloaded already`; the server answers `304 Not modified` with no body. Measured 2026-10-19 on Windows: the output file differs at byte 0 - Curl truncates it. curl opens the `-o` file lazily, on the first body byte, so a bodiless 304 leaves it untouched.

Measure real curl first (`Record-CurlExchange.ps1`, local curl 8.22.0 Schannel) with a pre-existing output file. Find where `-o` opens its file (`Curl.Console` or the CLI's output writer, `grep -rn "FileMode.Create"`); add that project to `touches` per the lane rules if it is not `Curl.Console`. Check what a lazy open does to `--create-dirs`, `--remove-on-error`, `--no-clobber` and an empty 200 body (curl creates an empty file for a 200 with no body at transfer end) before changing it.

## Acceptance criteria

- [ ] Notes record real curl's exit code and file contents for 1566, and a verdict (a), (b) or (c) as BL-2831 defines them.
- [ ] For (a) or (b), 1566 passes with `$env:CURL_UPSTREAM_CASE='1566'` and is on `PassingUpstreamCases.txt`.
- [ ] A product fix has a focused unit test in the fixed library's `.UnitTests` project that fails without it (ADR-0482).
- [ ] Build clean, fast tests green, the changed library keeps 100% line and branch coverage and complexity at most 10.

## Notes

## Log

- 2026-10-19: Created.
