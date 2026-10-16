---
id: BL-2438
title: Libcurl: CurlUrlHandle (get, set, dup, strerror)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2456, BL-2459]
touches: [Libcurl/Url, Libcurl.UnitTests/Url, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2438 — Libcurl: CurlUrlHandle (get, set, dup, strerror)

## Goal

Libcurl has `CurlUrlHandle` (`curl_url/_cleanup/_dup/_get/_set/_strerror`) over `ICurlUrlHandle`, with the `CurlUrlPart` and `CurlUrlFlags` values.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Fake-engine tests pin each call, and `CURLOPT_CURLU` uses it.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- `touches` gains `Libcurl/CLAUDE.md`: the acceptance criteria require it kept true, and it now describes `Libcurl/Url`. No other task in Doing names it.
- "`CURLOPT_CURLU` uses it" is BL-2462's work (it depends on this task and owns `Libcurl/Options/Network`), so it is left there (AF-0148). `CurlUrlHandle` exposes the internal `EngineHandle` BL-2462 needs.
- Decisions in ADR-0581: `curl_url` is `CurlLibrary.UrlInit()` (beside `EasyInit`/`MultiInit`, not shadowing the `Libcurl.Url` namespace); `Get`/`Set` pass straight through to the engine, which owns part and flag checks; `curl_url_strerror` is `CurlUrlHandle.ErrorText` over `CurlErrorText.Url`; a cleaned-up handle throws `ObjectDisposedException` like `CurlEasy`.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` 0 failing members; Libcurl.UnitTests 612 passed; full fast run green; `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. CurlUrlHandle (curl_url, _get, _set, _dup, _cleanup, _strerror) over ICurlUrlHandle with fake-engine tests; Libcurl quality 0 failing
