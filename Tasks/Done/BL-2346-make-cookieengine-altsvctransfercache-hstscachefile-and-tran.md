---
id: BL-2346
title: Make CookieEngine, AltSvcTransferCache, HstsCacheFile and TransferPerformer's cache steps read EasyHttpOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2307]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2346 — Make CookieEngine, AltSvcTransferCache, HstsCacheFile and TransferPerformer's cache steps read EasyHttpOptions

## Goal

`CookieEngine`, `AltSvcTransferCache`, `HstsCacheFile` and `TransferPerformer`'s cookie, HSTS and alt-svc steps (`LoadCookieFilesAsync`, `SwitchToHttpsForHstsAsync`, `OpenAltSvcCacheAsync`, `SetupFailureAsync`'s cookie-string check, `WriteCookieJarAltSvcAndHstsFilesAsync`), all in `Curl.Engine.UnitLibrary`, read the cookie, alt-svc and HSTS settings from `EasyHttpOptions` instead of `CommandLineOptions`; behaviour is byte for byte unchanged.

## Context

Split out of BL-2307 (ADR-0506 plan phase 1 step 4, ADR-0527), which built `EasyOptionsFromCommandLine.HttpOf`. Follow BL-2303's rules for retarget tasks; `Curl.Engine.UnitTests` already links `Curl.Console/EasyOptionsFromCommandLine.cs` (ADR-0526). Record members: `-b name=value` strings = `Cookie` (joined with `"; "`, as `--libcurl` writes `CURLOPT_COOKIE`; the engine's own join puts no space before a string starting with a blank, so check `CookieEngine`'s join against it and keep the engine's bytes, recording any difference in the ADR); `-b` files = `CookieFiles`; `-c` = `CookieJar`; `-j` = `CookieSession`; `--alt-svc` = `AltSvc` with `AltSvcControl` `H1|H2|H3`; `--hsts` = `Hsts` with `HstsControl.Enable`. `AltSvcTransferCache` also reads the HTTP version: use `EasyHttpOptions.HttpVersion` there if BL-2345 is Done, otherwise leave that one read.

## Acceptance criteria

- [x] The classes and steps named in Goal take `EasyHttpOptions` for their cookie, alt-svc and HSTS settings, not `CommandLineOptions`.
- [x] Tests that built `CommandLineOptions` only to drive them build the record; tests through the whole command line stay.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.

## Notes

- Plan (one pass, before code): every reader named in Goal is in `Curl.Engine.UnitLibrary`, with callers in `Curl.Console` (`CurlCommandRunner`, `CurlComposition`) and tests in the two test projects already in `touches`; BL-2345 is Done, so `AltSvcTransferCache` reads `EasyHttpOptions.HttpVersion`. No prerequisite was missing.
- Cookie join (ADR-0533): `HttpOf` joined the `-b` strings with `"; "` every time; the engine leaves the space out before a string starting with a space or tab, as curl's `cookie_setopts` does (BL-1391). curl hands that same joined string to `CURLOPT_COOKIE`, so `HttpOf` now joins the engine's way and `CookieEngine` uses `Cookie` verbatim, keeping the engine's bytes. New `EasyOptionsFromCommandLineTests` rows pin the space and tab cases.
- `LoadCookieFilesAsync` and `SetupFailureAsync` read no cookie setting from `CommandLineOptions` (their cookie settings come through `CookieEngine`), so their signatures are unchanged. `SwitchToHttpsForHstsAsync`, `OpenAltSvcCacheAsync` and `WriteCookieJarAltSvcAndHstsFilesAsync` take the record beside `CommandLineOptions`, which they still read for the `-G` query, `--path-as-is` and `--connect-to` (BL-2343).
- `HstsCacheFile.ReadAsync`/`WriteAsync` take the record and skip a `null` or empty `Hsts`, so the performer's own `--hsts` checks are gone. `HstsControl` is not read, because `HttpOf` sets it exactly when `Hsts` is set.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` gave both libraries 100% line and branch coverage. The one failing member, `CookieEngine..ctor` at complexity 12, was split into `SentCookieStringsOf` and `HandlerStoreOf` (each at most 4) and the tests re-ran green. The quality run was not repeated (rule 9); `CookieEngineTests` covers every branch of the split.
- Fast tests: every project passed (Curl.Engine.UnitTests 673, Curl.Console.UnitTests 2826, Curl.Conformance.UnitTests 3129 passed); `PassingUpstreamCases.txt` is untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. CookieEngine, AltSvcTransferCache, HstsCacheFile and the performer's cookie, HSTS and alt-svc steps read EasyHttpOptions; HttpOf joins -b strings as cookie_setopts does (ADR-0533)
