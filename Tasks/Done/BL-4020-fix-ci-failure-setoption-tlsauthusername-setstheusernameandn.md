---
id: BL-4020
title: Fix CI failure SetOption_TlsauthUsername_SetsTheUserNameAndNullClearsIt on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.IntegrationTests, Curl.Cli.UnitLibrary, Libcurl.UnitTests/Cgr265Tests.cs, Libcurl.UnitTests/Options/Authentication/EasyAuthenticationOptionSettersTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4020 — Fix CI failure SetOption_TlsauthUsername_SetsTheUserNameAndNullClearsIt on Windows

## Goal

`SetOption_TlsauthUsername_SetsTheUserNameAndNullClearsIt` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SetOption_TlsauthUsername_SetsTheUserNameAndNullClearsIt` failed on Windows in CI run 40803338913 (https://github.com/StewartScottRogers/Curl/actions/runs/40803338913). First failing commit: 62ebfe30.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40803338913 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `SetOption_TlsauthUsername_SetsTheUserNameAndNullClearsIt` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the test lives in Libcurl.UnitTests, not Curl.Cli. EasyAuthenticationOptionSettersTests sets the process-wide static `EasyAuthenticationOptionSetters.SrpIsBuiltIn` to true in TestInitialize, while Cgr265Tests sets it to false; tests run method-level parallel, so on Windows (where the default is false) a Cgr265 case could flip it mid-test and TLSAUTH_USERNAME answered NotBuiltIn. The failing commit (62ebfe30) only touched the board, which confirms a race rather than a regression.
- Fix: both classes that flip the flag are `[DoNotParallelize]`, so MSTest runs them serially after the parallel batch. Added the two Libcurl.UnitTests files to touches (no task in Doing on origin/work/dark-factory names them). Test-only change, no production code, so no quality run was needed. The same race is the cause of BL-4019 (ProxyTlsauthType), which this fix also covers.
- CI criterion: the change is platform-neutral (an attribute only) and the fast tests are green here (Libcurl.UnitTests 1504 passed, full fast run no failures); ticked per lane rule 11.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 1650aa5f.
- 2026-10-28: Doing -> Done. Tests that flip SrpIsBuiltIn run serially, so TLSAUTH setter tests no longer race on Windows
