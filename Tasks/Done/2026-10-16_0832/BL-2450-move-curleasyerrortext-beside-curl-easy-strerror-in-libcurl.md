---
id: BL-2450
title: Move CurlEasyErrorText beside curl_easy_strerror in Libcurl
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2449, BL-2459]
touches: [Libcurl/Misc/ErrorText, Libcurl.UnitTests/Misc/ErrorText, Curl.Console, Curl.Console.UnitTests, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2450 — Move CurlEasyErrorText beside curl_easy_strerror in Libcurl

## Goal

`CurlEasyErrorText` lives in `Libcurl/Misc/ErrorText` beside `CurlErrorText.Easy` (`curl_easy_strerror`), and `Curl.Console` and its tests use it from there; every text is unchanged.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Every `CURLE_*` text is pinned by a test against the old table's values, and the exit-code gap score is unchanged.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Console` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Moved `Curl.Console/CurlEasyErrorText.cs` to `Libcurl/Misc/ErrorText/CurlEasyErrorText.cs` (git rename), now `public` in `Libcurl.Misc.ErrorText`; the table's `[CurlExitCode.X] = "..."` lines are byte-for-byte unchanged, at the path BL-2449's `Measure-ExitCodeGap.ps1` reads first, so the exit-code gap score is unchanged (BL-2449 measured both layouts at 259/259; lanes cannot read `Gap/` to rerun it).
- Added `CurlErrorText.Easy(CurlCode)` (`curl_easy_strerror`) over it, converting with `CurlCodeConversion`. Kept the table keyed by `CurlExitCode` rather than `CurlCode`: the tool and engine use `CurlExitCode`, and the gap tool parses that line shape.
- `Curl.Console` now references `Libcurl` (no cycle: Libcurl references only Curl.Api.UnitLibrary and Curl.Engine.UnitLibrary). `Libcurl/CLAUDE.md` added to `touches` to keep it true (acceptance criterion); no task in Doing named it.
- Tests: `CurlEasyErrorTextTests` moved to `Libcurl.UnitTests/Misc/ErrorText` unchanged; new `CurlErrorTextTests.Easy_DefinedOrUnknownCode_AnswersLibcurlsTextOrFallback` pins all 87 texts, generated from the old table, plus `Obsolete20` and an out-of-range code as `Unknown error`.
- Results: build clean; Libcurl.UnitTests 1058, Curl.Console.UnitTests 2807 (25 skipped, as before), Curl.Conformance.UnitTests 3771 (PassingUpstreamCases.txt untouched), Curl.Engine.UnitTests 1554, all passing. `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Console`: 0 failing members.
- Left for BL-2484: `Curl.Engine.UnitLibrary` doc comments and CLAUDE.md, and the plan document, still say the table stays in `Curl.Console`; BL-2440 held the engine during this run.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. curl_easy_strerror's table lives in Libcurl/Misc/ErrorText as CurlEasyErrorText with CurlErrorText.Easy; Curl.Console uses it from there, every text unchanged
