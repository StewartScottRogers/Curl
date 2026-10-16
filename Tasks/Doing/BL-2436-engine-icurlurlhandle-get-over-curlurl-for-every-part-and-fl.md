---
id: BL-2436
title: Engine: ICurlUrlHandle get over CurlUrl for every part and flag
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2456]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2436 — Engine: ICurlUrlHandle get over CurlUrl for every part and flag

## Goal

The engine's `ICurlUrlHandle` gets URL, scheme, user, password, options, host, port, path, query, fragment and zone id, with `CURLU_DEFAULT_PORT`, `URLDECODE`, `URLENCODE`, `PUNYCODE`, `NO_DEFAULT_PORT`, `GET_EMPTY` and the rest, built on Curl's existing `CurlUrl` parser.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] A test per part and per flag, with upstream's `CurlUrlCode` results for missing parts.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
