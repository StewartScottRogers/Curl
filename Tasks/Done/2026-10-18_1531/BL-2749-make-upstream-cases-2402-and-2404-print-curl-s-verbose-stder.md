---
id: BL-2749
title: Make upstream cases 2402 and 2404 print curl's verbose stderr lines over HTTP/2 and hold them on the ratchet
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2749 — Make upstream cases 2402 and 2404 print curl's verbose stderr lines over HTTP/2 and hold them on the ratchet

## Goal

Upstream cases 2402 and 2404 (lib2402 and lib2404 over HTTP/2) pass through Curl and are held on the ratchet.

## Context

Found by BL-2697. Run with the Unix platform's features (http/2, h2c; the harness line `Platform` in `UpstreamConformanceTests` set to `UpstreamCurlPlatform.Unix` for one local run), both fail the same way: the verify file `stderr2402` / `stderr2404` differs at byte 0 (line 1): expected `* Connection #0 to host localhost:9018 left intact` followed by CRLF, got the end of the file, so the libtest program's verbose stderr is empty. Cases 2402 and 2404 are listed as skipped, with run time, on Linux and macOS CI (run 39155312605), which cannot say why. Port: `Curl.Conformance.LibTest.UnitLibrary/Lib2402.cs`, `LibPipeWaitHandles.cs`. Do not add a case to the ratchet before it passes on every platform that runs it (Windows skips http/2 cases).

## Acceptance criteria

- [x] Cases 2402 and 2404 pass when run with the Unix platform's features, and are on `PassingUpstreamCases.txt` without making Windows, Linux or macOS CI red (a focused unit test in `Curl.Conformance.LibTest.UnitTests` pins the fix).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Cause: lib2402 and lib2404 set CURLOPT_VERBOSE but no debug function, and the engine prints verbose lines only through a debug callback, so stderr stayed empty. Fix: `LibTestDefaultVerboseTrace` (libcurl's default `* `, `< `, `> ` lines, no tags, no data) set by `LibPipeWaitHandles.DefaultVerboseOptions` for lib2402 and lib2404; lib2502 keeps its own libtest_debug_cb. Both cases measured passing with the Unix platform (temporary harness edit, reverted). Windows skips them (no http/2), as it skips 2400. The engine ignoring CURLOPT_STDERR is a separate gap, not touched here.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: cb84e87e 3d836d5e.
- 2026-10-18: Doing -> Done. lib2402 and lib2404 print libcurl's default verbose lines on stderr; cases 2402 and 2404 pass and are on the ratchet
