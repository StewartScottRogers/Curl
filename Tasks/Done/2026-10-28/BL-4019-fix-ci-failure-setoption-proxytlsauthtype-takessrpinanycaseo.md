---
id: BL-4019
title: Fix CI failure SetOption_ProxyTlsauthType_TakesSrpInAnyCaseOrNullAndRefusesAnythingElse on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.slnx, Libcurl.UnitTests/Cgr265Tests.cs, Libcurl.UnitTests/Options/Authentication/EasyAuthenticationOptionSettersTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4019 — Fix CI failure SetOption_ProxyTlsauthType_TakesSrpInAnyCaseOrNullAndRefusesAnythingElse on Windows

## Goal

`SetOption_ProxyTlsauthType_TakesSrpInAnyCaseOrNullAndRefusesAnythingElse` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `SetOption_ProxyTlsauthType_TakesSrpInAnyCaseOrNullAndRefusesAnythingElse` failed on Windows in CI run 40803338913 (https://github.com/StewartScottRogers/Curl/actions/runs/40803338913). First failing commit: 62ebfe30.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40803338913 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `SetOption_ProxyTlsauthType_TakesSrpInAnyCaseOrNullAndRefusesAnythingElse` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes
- Cause: `EasyAuthenticationOptionSetters.SrpIsBuiltIn` is one mutable static (false on Windows, the Schannel build). `EasyAuthenticationOptionSettersTests` sets it true per test and `Cgr265Tests` sets it false, and MSTest ran them in parallel, so on Windows a TLSAUTH setter sometimes read false mid-test and answered NotBuiltIn (CI run 40803338913: the ProxyTlsauthType and TlsauthUsername tests). The same race could hit Linux and macOS the other way round. The first failing commit, 62ebfe30, only changed task files; the race was already there.
- Fix: `[DoNotParallelize]` on both classes, the only tests that touch the flag or any TLSAUTH option, so nothing else runs while they change it. I chose a test-only fix over threading the flag through the dispatch: there is no production change and so no quality gate impact, and the fix works on every platform.
- Touches: added the two test files (Curl.slnx was a placeholder from the CI watch). No task in Doing on origin/work/dark-factory names them.
- Verified: Libcurl.UnitTests fast tests 1504 passed, 0 failed on Windows. The CI criterion is met because the change is platform-neutral and the fast tests are green here (lane rule 11).

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 016096a0.
- 2026-10-28: Doing -> Done. The TLS-SRP setter tests no longer race over SrpIsBuiltIn: both classes are DoNotParallelize
