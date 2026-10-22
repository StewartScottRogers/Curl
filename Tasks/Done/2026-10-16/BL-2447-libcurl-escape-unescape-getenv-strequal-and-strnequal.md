---
id: BL-2447
title: Libcurl: escape, unescape, getenv, strequal and strnequal
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2456]
touches: [Libcurl/Misc/Strings, Libcurl.UnitTests/Misc/Strings]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2447 — Libcurl: escape, unescape, getenv, strequal and strnequal

## Goal

Libcurl has `CurlStrings.Escape/Unescape` (`curl_easy_escape/_unescape` and the deprecated `curl_escape/_unescape`), `GetEnv`, `StrEqual` and `StrNEqual`.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Tests pin upstream's results, including a zero-length input and `%00` in unescape.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Built as `Libcurl/Misc/Strings/CurlStrings.cs`, decided in ADR-0595: text read as UTF-8 bytes with byte lengths; `Unescape` answers `byte[]` so `%00` and high bytes survive; `null` text, a negative length or one past the end answers `null` (C would read past the buffer, so refusing is the safe default); `GetEnv` answers `null` for unset or empty, with an internal lookup overload so tests leave the process environment alone; `StrEqual`/`StrNEqual` answer `bool` for 1/0.
- `Libcurl/CLAUDE.md` gained a `Misc/Strings` paragraph (the criterion asks it be kept true; not in `touches`, no other Doing task is working that paragraph).
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` 0 failing members; fast tests all green (Libcurl.UnitTests 826).

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Libcurl CurlStrings: escape/unescape (and deprecated forms), getenv, strequal, strnequal
