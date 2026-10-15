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
completed:
---
# BL-2346 — Make CookieEngine, AltSvcTransferCache, HstsCacheFile and TransferPerformer's cache steps read EasyHttpOptions

## Goal

`CookieEngine`, `AltSvcTransferCache`, `HstsCacheFile` and `TransferPerformer`'s cookie, HSTS and alt-svc steps (`LoadCookieFilesAsync`, `SwitchToHttpsForHstsAsync`, `OpenAltSvcCacheAsync`, `SetupFailureAsync`'s cookie-string check, `WriteCookieJarAltSvcAndHstsFilesAsync`), all in `Curl.Engine.UnitLibrary`, read the cookie, alt-svc and HSTS settings from `EasyHttpOptions` instead of `CommandLineOptions`; behaviour is byte for byte unchanged.

## Context

Split out of BL-2307 (ADR-0506 plan phase 1 step 4, ADR-0527), which built `EasyOptionsFromCommandLine.HttpOf`. Follow BL-2303's rules for retarget tasks; `Curl.Engine.UnitTests` already links `Curl.Console/EasyOptionsFromCommandLine.cs` (ADR-0526). Record members: `-b name=value` strings = `Cookie` (joined with `"; "`, as `--libcurl` writes `CURLOPT_COOKIE`; the engine's own join puts no space before a string starting with a blank, so check `CookieEngine`'s join against it and keep the engine's bytes, recording any difference in the ADR); `-b` files = `CookieFiles`; `-c` = `CookieJar`; `-j` = `CookieSession`; `--alt-svc` = `AltSvc` with `AltSvcControl` `H1|H2|H3`; `--hsts` = `Hsts` with `HstsControl.Enable`. `AltSvcTransferCache` also reads the HTTP version: use `EasyHttpOptions.HttpVersion` there if BL-2345 is Done, otherwise leave that one read.

## Acceptance criteria

- [ ] The classes and steps named in Goal take `EasyHttpOptions` for their cookie, alt-svc and HSTS settings, not `CommandLineOptions`.
- [ ] Tests that built `CommandLineOptions` only to drive them build the record; tests through the whole command line stay.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.

## Notes

## Log

- 2026-10-15: Created.
