---
id: BL-2419
title: Libcurl: prerequest, resolver-start and trailer callbacks wired through the engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2418]
touches: [Libcurl/Callbacks/Phases, Libcurl.UnitTests/Callbacks/Phases, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2419 — Libcurl: prerequest, resolver-start and trailer callbacks wired through the engine

## Goal

CURLOPT_PREREQFUNCTION, RESOLVER_START_FUNCTION and TRAILERFUNCTION are called by the engine at the point upstream calls them, with the connection info and trailer list.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests pin each call point and the abort return.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Split (decided by Claude under Stewart's delegation, ADR-0611): the trailer callback needs a request-trailer seam in `Curl.Protocol.Abstractions` and work in `Curl.Protocol.Http`'s `HttpRequestBodyWriter`, which writes a fixed `0\r\n\r\n` last chunk today; both are outside this task's `touches`, so BL-2475 does it and adds the `TRAILERFUNCTION` rows. Until then `TRAILERFUNCTION`/`TRAILERDATA` keep answering `NotBuiltIn` (pinned by `EasyPhasesCallbackSettersTests`), so no option is accepted and ignored.
- Prerequest: `PrerequestConnector` over the TCP handlers' pool and the connect-only connector, once per connect (once per request; a followed redirect again), abort exit 42 `operation aborted by pre-request callback` (curl 8.22.0 `multi.c` `DO` state). Resolver start: `ResolverStartDnsResolver` under the TCP and UDP connectors through a new `EngineComposition.CreateTransports` overload taking `wrapLookUps` (the old overload is unchanged, so `Curl.Console` and its crefs need no edit); runs only on a DNS cache miss, abort is exit 6 `Could not resolve host: <h>` and nothing cached (upstream test655 expects `CURLE_COULDNT_RESOLVE_HOST`).
- Upstream source is not in the repository; the call points and texts are from curl 8.22.0's `lib/multi.c` and `lib/hostip.c` as recalled, not measured with Record-CurlExchange.ps1 (it drives the tool, which has no option for either callback).
- `Libcurl/CLAUDE.md` gained one sentence for `Callbacks/Phases`, as the acceptance criteria ask.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` reports 0 failing members; full fast run green.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Prerequest and resolver-start callbacks wired through the engine and set by Libcurl; trailer callback split to BL-2475
