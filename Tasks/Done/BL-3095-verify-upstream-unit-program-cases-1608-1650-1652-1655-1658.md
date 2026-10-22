---
id: BL-3095
title: Verify upstream unit-program cases 1608, 1650, 1652, 1655, 1658, 2413, 3219, 3302, 3303 and 3304 pass on Windows and keep the gates
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2886, BL-2922, BL-2923, BL-2924, BL-3014, BL-3015, BL-3016, BL-3017, BL-2921, BL-3062]
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3095 — Verify upstream unit-program cases 1608, 1650, 1652, 1655, 1658, 2413, 3219, 3302, 3303 and 3304 pass on Windows and keep the gates

## Goal

Each of the ten cases 1608, 1650, 1652, 1655, 1658, 2413, 3219, 3302, 3303 and 3304 ends Passed on Windows and every gate holds.

## Context

Split from BL-2847 (over its cost cap). All ten cases are already ported and on `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` (1608, 1650, 1652, 1655, 1658, 2413, 3219, 3302, 3303, 3304); BL-3062 ported 1650. What is left is confirming, with no new code expected.

## Acceptance criteria

- [x] Each of the ten cases ends `Passed` with `$env:CURL_UPSTREAM_CASE='<n>'` on Windows and is on `PassingUpstreamCases.txt`.
- [x] Each of the ten cases has a port in `Curl.Conformance.UnitProgram.UnitLibrary` registered in `UnitPrograms.Ported`, with a test in `Curl.Conformance.UnitProgram.UnitTests`, or a Notes entry naming the C-only thing it tests.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and each changed library and the new library keep 100% line and branch coverage and complexity at most 10.

## Notes

Split from BL-2847. Run order: this first, then BL-3096 checks CI.

Verified 2026-10-22: all ten cases Passed via CURL_UPSTREAM_CASE on Windows; each has a registered port in UnitPrograms.Ported and a test; no code changed, so no coverage re-measure; build clean, fast tests green.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 08c8fe91 6f3a4d7b c0723528.
- 2026-10-22: Doing -> Done. Ten upstream unit-program cases verified Passed on Windows, ported and on the ratchet; build and fast tests green
