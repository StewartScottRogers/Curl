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
completed: 2026-10-15
---
# BL-2399 — Libcurl: the options table (options.h) and the SetOption dispatch

## Goal

Libcurl has `CurlOption` (every `CURLOPT_*` by name and id), `CurlOptionType` (`CURLOT_*`), the `CurlEasyOption` record, `CurlOptions.ByName/ById/Next` (`curl_easy_option_by_name/_by_id/_next`, `CURLOT_FLAG_ALIAS`) and the one table `SetOption` dispatches through: each group task adds its rows from its own folder `Libcurl/Options/<Group>`, and an option no group has yet answers `CurlCode.UnknownOption` for an id upstream lacks or `CurlCode.NotBuiltIn` for one still to come.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] The table lists every option of curl 8.22.0 `options.h` (the count is pinned by a test) with its type, and `by_name` matches case-insensitively as upstream does.
- [x] `SetOption` returns `CurlCode.BadFunctionArgument` for a value of the wrong type, with a test per `CURLOT_*` type.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan: generate `CurlOption` and the 327-row table (312 options, 15 aliases) from curl 8.22.0's `lib/easyoptions.c` and `include/curl/curl.h`, fetched from GitHub at tag `curl-8_22_0` (the gap office's tarball cache is an audit path a lane may not read). `CurlOptions.All/ByName/ById/Next` are public; `EasyOptionDispatch.SetOption` is internal, for `CurlEasy` (BL-2408) to call through `EasyOptionDispatch.Default`. Decisions in ADR-0558.
- `CurlOption` member names follow `CurlCode`'s rule (one capital per underscore-separated word: `AccepttimeoutMs`); hand-picked word breaks would be 327 choices nobody can check.
- Value types: `LONG`/`OFF_T` take `long` or `int`, `VALUES` also any `Enum`, `OBJECT`/`CBPTR` anything but a `Delegate` (so each `CURLOT_*` type has a wrong-type test), pointer types take `null`; `bool` is not accepted (libcurl takes `0L`/`1L`).
- Group tasks (BL-2400 to BL-2407, and the callback tasks) implement `Add<Group>Setters` in an `EasyOptionSetters` part in their own `Libcurl/Options/<Group>` folder, keeping namespace `Libcurl.Options`; the declarations already sit in `Libcurl/Options/EasyOptionSetters.cs`.
- `Libcurl/CLAUDE.md` is outside `touches`, but the acceptance criteria name it; one paragraph was added for the Options area.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members; Libcurl.UnitTests 74 pass; the fast tests had 0 failures across 40 assemblies; `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Libcurl has CurlOption, CurlOptions.ByName/ById/Next over curl 8.22.0's 327 rows, and the SetOption dispatch with per-type checks
