---
id: BL-2411
title: Libcurl: CURLOPT_ERRORBUFFER and CurlEasy.ErrorText
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2400, BL-2408]
touches: [Libcurl/Easy, Libcurl.UnitTests/Easy]
requirement: none
created: 2026-10-15
completed:
---
# BL-2411 — Libcurl: CURLOPT_ERRORBUFFER and CurlEasy.ErrorText

## Goal

A failed `Perform` fills the 256-byte error buffer set through `CURLOPT_ERRORBUFFER` with the engine's text (cut by the engine's `CurlErrorBuffer`), and clears it on the next successful call as upstream does.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] Tests pin the buffer cut at 255 bytes plus terminator, the clear on success, and a handle with no buffer set.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

## Log

- 2026-10-15: Created.
