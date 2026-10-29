---
id: BL-4030
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4030 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 40810061535 (https://github.com/StewartScottRogers/Curl/actions/runs/40810061535). First failing commit: 6407f459.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; ManagedMultiTransfer.TimerMilliseconds() is outside a gate: complexity; ManagedMultiTransfer.TellWaitedSocket(Curl.Api.EngineContracts.Multi.MultiSocketCallback, Curl.Engine.ManagedMultiTransfer.MultiEntry, Curl.Engine.TransferSocketWait) is outside a gate: complexity; ManagedMultiTransfer.IsWaitingOnAnEngineSocket(Curl.Engine.ManagedMultiTransfer.MultiEntry) is outside a gate: branch; ManagedMultiTransfer.IsWaitingWithoutSocket(Curl.Engine.ManagedMultiTransfer.MultiEntry) is outside a gate: branch

Lanes test only on Windows, so reproduce with `gh run view 40810061535 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- 2026-10-28 (lane 3): Already fixed on the branch, no code change. The four members CI named at 6407f459 were reworked by e12307969 (socket-wait predicates moved onto TransferSocketWait) and ca399a757 (BL-4038). `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` at b37595366: Curl.Engine.UnitLibrary 100% line, 100% branch, max complexity 10, 0 failing members; every test project it ran passed. CI on Linux and macOS: no change lands, so nothing platform-dependent is added; the shift's CI watch files a task for any failure.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 10cc6e68.
- 2026-10-28: Doing -> Done. Quality gates pass for Curl.Engine.UnitLibrary at HEAD (100/100, complexity 10, 0 failing); fixed by e12307969 and ca399a757
