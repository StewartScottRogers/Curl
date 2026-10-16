---
id: BL-2399
title: Libcurl: the options table (options.h) and the SetOption dispatch
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2456]
touches: [Libcurl/Options, Libcurl.UnitTests/Options]
requirement: none
created: 2026-10-15
completed:
---
# BL-2399 — Libcurl: the options table (options.h) and the SetOption dispatch

## Goal

Libcurl has `CurlOption` (every `CURLOPT_*` by name and id), `CurlOptionType` (`CURLOT_*`), the `CurlEasyOption` record, `CurlOptions.ByName/ById/Next` (`curl_easy_option_by_name/_by_id/_next`, `CURLOT_FLAG_ALIAS`) and the one table `SetOption` dispatches through: each group task adds its rows from its own folder `Libcurl/Options/<Group>`, and an option no group has yet answers `CurlCode.UnknownOption` for an id upstream lacks or `CurlCode.NotBuiltIn` for one still to come.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] The table lists every option of curl 8.22.0 `options.h` (the count is pinned by a test) with its type, and `by_name` matches case-insensitively as upstream does.
- [ ] `SetOption` returns `CurlCode.BadFunctionArgument` for a value of the wrong type, with a test per `CURLOT_*` type.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
