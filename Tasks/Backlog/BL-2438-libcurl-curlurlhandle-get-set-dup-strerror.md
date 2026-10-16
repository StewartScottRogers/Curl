---
id: BL-2438
title: Libcurl: CurlUrlHandle (get, set, dup, strerror)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2456, BL-2459]
touches: [Libcurl/Url, Libcurl.UnitTests/Url]
requirement: none
created: 2026-10-15
completed:
---
# BL-2438 — Libcurl: CurlUrlHandle (get, set, dup, strerror)

## Goal

Libcurl has `CurlUrlHandle` (`curl_url/_cleanup/_dup/_get/_set/_strerror`) over `ICurlUrlHandle`, with the `CurlUrlPart` and `CurlUrlFlags` values.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] Fake-engine tests pin each call, and `CURLOPT_CURLU` uses it.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

## Log

- 2026-10-15: Created.
