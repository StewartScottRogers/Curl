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
completed:
---
# BL-4023 — Cgr265Tests races EasyAuthenticationOptionSettersTests over the static SrpIsBuiltIn

## Goal

Libcurl.UnitTests passes every run on every platform: no test changes `EasyAuthenticationOptionSetters.SrpIsBuiltIn` while another test reads it.

## Context

`EasyAuthenticationOptionSetters.SrpIsBuiltIn` (Libcurl/Options/Authentication/EasyAuthenticationOptionSetters.cs, BL-3794) is a settable static. `EasyAuthenticationOptionSettersTests` sets it `true` in its test initialize and `Cgr265Tests` sets it `false`; tests run method-level parallel (MSTestSettings.cs), so on 2026-10-28 BL-4017's quality run on Windows failed `SetOption_TlsauthType_TakesSrpInAnyCaseOrNullAndRefusesAnythingElse`, `SetOption_TlsauthUsername_SetsTheUserNameAndNullClearsIt` and `SetOption_TlsauthPassword_SetsThePasswordAndNullClearsIt` with `NotBuiltIn` where `Ok` was expected. The CI quality job fails whenever this race goes the wrong way. Replace the static with state the tests can give per call (for example an injected flag on the setter table or `EasyOptionDispatch`), or mark both classes `[DoNotParallelize]`.

## Acceptance criteria

- [ ] No test in Libcurl.UnitTests assigns `EasyAuthenticationOptionSetters.SrpIsBuiltIn` (or its replacement) as shared static state while tests run in parallel.
- [ ] `dotnet test Libcurl.UnitTests --filter "TestCategory!=Integration"` passes, and Libcurl stays at 100% line and branch coverage.

## Notes

- Filed by BL-4017's run, which met the failure while measuring Curl.Core and Curl.Engine coverage.

## Log

- 2026-10-28: Created.
