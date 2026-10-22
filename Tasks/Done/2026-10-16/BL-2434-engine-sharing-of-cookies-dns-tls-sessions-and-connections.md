---
id: BL-2434
title: Engine: sharing of cookies, DNS, TLS sessions and connections
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2433]
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2434 — Engine: sharing of cookies, DNS, TLS sessions and connections

## Goal

The managed engine's `IShareHandle` shares cookie jars, DNS results, TLS session tickets and the connection pool between attached easy transfers, calling the lock callbacks around access.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests show a cookie set by one handle sent by another, and a connection reused across two handles.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Plan (ADR-0606): `ManagedShareHandle` owns its own cookie, DNS, TLS session, connection and HSTS caches. A perform whose `CURLOPT_SHARE` names it attaches for the perform and runs over a `ManagedEngineSharedState` with the share's cache in place of the engine's for each kind shared. ADR-0542 already shares every cache between one engine's handles, so the tests show sharing between handles of two engines.
- Lock callbacks run as synchronous lock/unlock pairs, never held across an await (the transfers are async, and a .NET Monitor must be released on the thread that took it): `Share` around attach, detach and cleanup, each shared kind as its cache is taken, and `Cookie` around every handler call on the shared cookies. Cookie file load and jar write run unlocked.
- `PSL` is accepted and shares nothing; the engine keeps no public suffix list.
- `ManagedCurlEngineTests.ShareMimeAndForm_NotYetBuilt_ThrowNotSupported` pinned the old throw. It is split into `CreateShare_Called_GivesAManagedShareHandle` and `MimeAndForm_NotYetBuilt_ThrowNotSupported`, so no assertion was weakened.
- Measured: Measure-CodeQuality first reported one branch gap in `HandlerCookieStoreOf` (a shared perform with no cookie option). `PerformAsync_CookiesSharedButNoCookieOption_SendsNoCookie` covers it. Engine tests: 1381 passed, 3 skipped. Full fast run green. `PassingUpstreamCases.txt` untouched.

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. The engine's share handle shares cookies, DNS, TLS sessions, connections and HSTS between attached transfers of any engine, calling the lock callbacks around access
