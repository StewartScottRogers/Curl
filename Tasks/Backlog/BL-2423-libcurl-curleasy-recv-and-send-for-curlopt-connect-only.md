---
id: BL-2423
title: Libcurl: CurlEasy.Recv and Send for CURLOPT_CONNECT_ONLY
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2408, BL-2402]
touches: [Libcurl/Easy, Libcurl.UnitTests/Easy, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2423 — Libcurl: CurlEasy.Recv and Send for CURLOPT_CONNECT_ONLY

## Goal

`CurlEasy.Send` and `Recv` (`curl_easy_send`/`curl_easy_recv`) work after a `CONNECT_ONLY` perform, `CurlCode.Again` when nothing is ready, over `IConnectOnlyConnection`, implemented by the managed engine.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [ ] Engine tests show a connect-only perform, a send and a recv over a fake connection; Libcurl tests show `Again` and the wrong-state result.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

## Log

- 2026-10-15: Created.
