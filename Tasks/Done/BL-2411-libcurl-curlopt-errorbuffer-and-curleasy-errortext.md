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
completed: 2026-10-15
---
# BL-2411 — Libcurl: CURLOPT_ERRORBUFFER and CurlEasy.ErrorText

## Goal

A failed `Perform` fills the 256-byte error buffer set through `CURLOPT_ERRORBUFFER` with the engine's text (cut by the engine's `CurlErrorBuffer`), and clears it on the next successful call as upstream does.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Tests pin the buffer cut at 255 bytes plus terminator, the clear on success, and a handle with no buffer set.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Decision (rule 1): the 255-byte cut is Libcurl's own `Libcurl/Easy/EasyErrorBuffer.Cut`, the same algorithm as the engine's internal `CurlErrorBuffer.Truncate`. The engine class is `internal` and `Curl.Engine.UnitLibrary` is outside this task's `touches`, and `ManagedEasyTransfer.ErrorText` is not cut, so the plan's "cut by the engine's CurlErrorBuffer" is met by the same rule in Libcurl rather than by an `InternalsVisibleTo` change to the engine.
- A perform clears the buffer whatever its result (libcurl clears it when a transfer starts) and writes the cut text only on failure; with no buffer set nothing is written, and `CurlEasy.ErrorText` answers the cut text either way.
- Whether libcurl falls back to `curl_easy_strerror` text when no detail was written is left to BL-2463 (needs BL-2450's move of `CurlEasyErrorText`).
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` reported 0 failing members; full build clean, fast tests green (no failing assembly). `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Perform fills and clears CURLOPT_ERRORBUFFER with a 255-byte cut; CurlEasy.ErrorText added; 0 failing members
