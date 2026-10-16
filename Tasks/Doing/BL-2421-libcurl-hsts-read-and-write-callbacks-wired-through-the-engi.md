---
id: BL-2421
title: Libcurl: HSTS read and write callbacks wired through the engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2420]
touches: [Libcurl/Callbacks/Hsts, Libcurl.UnitTests/Callbacks/Hsts, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2421 — Libcurl: HSTS read and write callbacks wired through the engine

## Goal

CURLOPT_HSTSREADFUNCTION and HSTSWRITEFUNCTION (`HstsEntry`, `HstsResult`) are called by the engine when the HSTS cache loads and saves.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] Engine tests pin the load of several entries and the save-on-cleanup call.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
