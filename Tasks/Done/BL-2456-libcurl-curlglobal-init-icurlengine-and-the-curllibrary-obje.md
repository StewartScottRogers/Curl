---
id: BL-2456
title: Libcurl: CurlGlobal.Init(ICurlEngine) and the CurlLibrary object that creates handles
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Global, Libcurl.UnitTests/Global, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2456 — Libcurl: CurlGlobal.Init(ICurlEngine) and the CurlLibrary object that creates handles

## Goal

Libcurl has `CurlGlobal.Init(ICurlEngine engine, CurlGlobalFlags flags)` returning a disposable library object (`CurlLibrary`) that holds the engine, honours `CURL_GLOBAL_*` flags, counts init/cleanup like `curl_global_init`/`curl_global_cleanup`, and creates `CurlEasy`, `CurlMulti`, `CurlShare`, `CurlUrlHandle`, `CurlMime` and `CurlForm` handles from the engine (the creating methods may throw until their area task lands, each naming that area).

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A unit test with a fake `ICurlEngine` shows Init, flag handling, double cleanup and handle creation delegating to the engine.
- [x] `curl_global_init_mem` (`CurlGlobal.InitMemory`) is accepted and its allocation callbacks ignored, documented in its XML doc.
- [x] `curl_global_sslset` and `curl_global_trace` are present and answer as the Schannel build does, each with a test.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Decisions (ADR-0555): one `CurlLibrary` per engine; its `DisposeAsync` is `curl_global_cleanup` (counts down once, disposes the engine; a second cleanup does nothing); `CurlGlobal.InitCount` is libcurl's `initialized` counter. The handle types do not exist yet and their tasks depend on this one, so instead of public creators that only throw, `CurlLibrary` is `partial` with internal `CreateEasyTransfer`, `CreateMultiTransfer`, `CreateShareHandle`, `CreateUrlHandle`, `CreateMimeBuilder` and `CreateFormBuilder` delegating to the engine (and throwing `ObjectDisposedException` after cleanup); each area task adds its public creator as a `CurlLibrary` part in its own folder.
- `InitMemory` takes five allocation delegates, throws `ArgumentNullException` naming a missing one (libcurl answers `CURLE_FAILED_INIT`) and never calls them. `SslSet` answers as Schannel (8, `schannel`) on Windows and OpenSSL (1, `openssl`) elsewhere: `Ok` for a matching id or case-insensitive name, else `TooLate`, `available` always the one backend. `Trace` answers `Ok` for any configuration.
- Added `Libcurl/CLAUDE.md` to `touches`: the last criterion asks to keep it true and it said Libcurl was empty of logic; no other Doing task (BL-2380 touches only Curl.Mcp) names it.
- Measured: `Measure-CodeQuality.ps1 -Library Libcurl` 100% line, 100% branch, max complexity 4, 0 failing members. Fast tests all green (Libcurl.UnitTests 32 tests). `PassingUpstreamCases.txt` untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. CurlGlobal.Init/InitMemory return a CurlLibrary over an injected engine that counts init and cleanup and creates every handle kind through it; SslSet and Trace answer as the Schannel build
