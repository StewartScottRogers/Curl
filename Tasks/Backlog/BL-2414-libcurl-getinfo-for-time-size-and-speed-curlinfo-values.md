---
id: BL-2414
title: Libcurl: GetInfo for time, size and speed CURLINFO values
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2412]
touches: [Libcurl/Info/Times, Libcurl.UnitTests/Info/Times]
requirement: none
created: 2026-10-15
completed:
---
# BL-2414 — Libcurl: GetInfo for time, size and speed CURLINFO values

## Goal

Every time (double and `_T` microsecond), size, speed and content-length `CURLINFO_*` (`TOTAL_TIME(_T)` ... `RETRY_AFTER`, `SIZE_UPLOAD(_T)`, `SPEED_DOWNLOAD(_T)`, `CONTENT_LENGTH_*`) is answered by `GetInfo`, the double forms derived from the same microsecond value as upstream.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] A test per info; a double and its `_T` twin agree.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

## Log

- 2026-10-15: Created.
