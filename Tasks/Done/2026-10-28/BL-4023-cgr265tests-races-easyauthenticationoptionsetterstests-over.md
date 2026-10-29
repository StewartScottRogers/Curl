---
id: BL-4023
title: Cgr265Tests races EasyAuthenticationOptionSettersTests over the static SrpIsBuiltIn
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl.UnitTests/Cgr265Tests.cs, Libcurl.UnitTests/Options/Authentication/EasyAuthenticationOptionSettersTests.cs, Libcurl/Options/Authentication/EasyAuthenticationOptionSetters.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4023 — Cgr265Tests races EasyAuthenticationOptionSettersTests over the static SrpIsBuiltIn

## Goal

Libcurl.UnitTests passes every run on every platform: no test changes `EasyAuthenticationOptionSetters.SrpIsBuiltIn` while another test reads it.

## Context

`EasyAuthenticationOptionSetters.SrpIsBuiltIn` (Libcurl/Options/Authentication/EasyAuthenticationOptionSetters.cs, BL-3794) is a settable static. `EasyAuthenticationOptionSettersTests` sets it `true` in its test initialize and `Cgr265Tests` sets it `false`; tests run method-level parallel (MSTestSettings.cs), so on 2026-10-28 BL-4017's quality run on Windows failed `SetOption_TlsauthType_TakesSrpInAnyCaseOrNullAndRefusesAnythingElse`, `SetOption_TlsauthUsername_SetsTheUserNameAndNullClearsIt` and `SetOption_TlsauthPassword_SetsThePasswordAndNullClearsIt` with `NotBuiltIn` where `Ok` was expected. The CI quality job fails whenever this race goes the wrong way. Replace the static with state the tests can give per call (for example an injected flag on the setter table or `EasyOptionDispatch`), or mark both classes `[DoNotParallelize]`.

## Acceptance criteria

- [x] No test in Libcurl.UnitTests assigns `EasyAuthenticationOptionSetters.SrpIsBuiltIn` (or its replacement) as shared static state while tests run in parallel.
- [x] `dotnet test Libcurl.UnitTests --filter "TestCategory!=Integration"` passes, and Libcurl stays at 100% line and branch coverage.

## Notes

- Filed by BL-4017's run, which met the failure while measuring Curl.Core and Curl.Engine coverage.
- Already met when claimed: BL-4020 (3f3c6209a) marked Cgr265Tests and EasyAuthenticationOptionSettersTests `[DoNotParallelize]`. MSTest runs those after the parallel tests, one at a time, so no assignment to SrpIsBuiltIn overlaps a read of it. No other Libcurl.UnitTests source sets the TLSAUTH options. No code changed. Kept the attribute rather than injecting a flag: it is the simpler fix and the task names it as an option.
- Verified 2026-10-28: Libcurl.UnitTests fast tests gave 1504 passed and 1 skipped. Measure-CodeQuality -Library Libcurl gave line 100%, branch 100% and 0 failing members.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: dca8ce74 bba61989.
- 2026-10-28: Doing -> Done. Already fixed by BL-4020's [DoNotParallelize]; Libcurl.UnitTests green, Libcurl at 100% line and branch
