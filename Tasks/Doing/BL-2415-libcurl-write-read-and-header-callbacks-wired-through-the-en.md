---
id: BL-2415
title: Libcurl: write, read and header callbacks wired through the engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2408, BL-2406]
touches: [Libcurl/Callbacks/Data, Libcurl.UnitTests/Callbacks/Data, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2415 — Libcurl: write, read and header callbacks wired through the engine

## Goal

CURLOPT_WRITEFUNCTION, READFUNCTION and HEADERFUNCTION (and their pointer-less delegate forms) set through `SetOption`, and the managed engine calls them for body, upload and header bytes with upstream's return values (`CURL_WRITEFUNC_PAUSE`, `CURL_READFUNC_ABORT`, a short write failing with `WriteError`).

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] Engine tests transfer through a loopback-free fake connection and show the callbacks receive the exact bytes; Libcurl tests pin the option wiring.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
