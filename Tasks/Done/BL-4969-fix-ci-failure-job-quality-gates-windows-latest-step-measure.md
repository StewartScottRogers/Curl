---
id: BL-4969
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1, Curl.Engine.UnitLibrary/AltSvcTransferCache.cs, Curl.Engine.UnitTests/AltSvcTransferCacheTests.cs, Curl.Console.UnitTests/CurlCommandRunnerAltSvcTests.cs]
requirement: none
created: 2026-11-11
completed: 2026-11-11
---
# BL-4969 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 43060203629 (https://github.com/StewartScottRogers/Curl/actions/runs/43060203629). First failing commit: d2e24184.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; <>c__DisplayClass272_0.<TransferUploadingAsync>b__1(Curl.Protocol.Abstractions.CurlUrl, Curl.Protocol.Abstractions.HttpRequestOptions) is outside a gate: branch; AltSvcTransferCache.SwitchedVersion(Curl.Core.AltSvc.AltSvcMatch, Curl.Protocol.Abstractions.HttpVersionPreference) is outside a gate: branch; AltSvcTransferCache.SameDestinationVersion(Curl.Core.AltSvc.AltSvcAlpn, Curl.Protocol.Abstractions.HttpVersionPreference) is outside a gate: branch

Lanes test only on Windows, so reproduce with `gh run view 43060203629 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Cause: BL-4912 pinned the alt-svc tests off Windows, so the Windows quality job never reached the h2/h3 branches of AltSvcTransferCache and the redirect-hop lambda in CurlCommandRunner. Fix: AltSvcTransferCache.OpenAsync takes an optional onWindows (default: the running system), the alt-svc tests pin it instead of an OSCondition, and a platform-neutral redirect-hop test was added. Measure-CodeQuality.ps1 on Curl.Engine.UnitLibrary and Curl.Console: 0 failing members. CI on Linux and macOS: tests are now platform-neutral and green here.

## Log

- 2026-11-11: Created.
- 2026-11-11: Backlog -> Doing. Criteria at claim: 10cc6e68.
- 2026-11-11: Doing -> Done. Alt-svc tests pin the platform through onWindows instead of skipping on Windows; quality gate shows 0 failing members for Engine and Console.
