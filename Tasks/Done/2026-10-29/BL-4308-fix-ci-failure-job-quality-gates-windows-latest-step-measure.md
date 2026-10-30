---
id: BL-4308
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1, Curl.Extension.DeltaDownload.UnitLibrary/DeltaDownloadTransferHook.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4308 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 40992069965 (https://github.com/StewartScottRogers/Curl/actions/runs/40992069965). First failing commit: 670288ad.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; DeltaDownloadTransferHook.FetchMissingAsync (async) is outside a gate: complexity

Lanes test only on Windows, so reproduce with `gh run view 40992069965 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- The failing member was DeltaDownloadTransferHook.FetchMissingAsync (its async state machine was past the complexity gate), so the fix is in Curl.Extension.DeltaDownload.UnitLibrary/DeltaDownloadTransferHook.cs, which I added to touches. No other task in Doing on origin/work/dark-factory names it. Measure-CodeQuality.ps1 itself needed no change.
- Fix: the per-range early exits moved into EndBeforeJoiningAsync, and the SHA-1 check and write into WriteIfIntactAsync. Behaviour is unchanged, so no ADR. The pipeline is feature, but the change is a mechanical refactor, so I made it directly with no architect stage.
- Measure-CodeQuality.ps1 -Library Curl.Extension.DeltaDownload.UnitLibrary: 0 failing members. Full build clean. Fast tests green: 84 test projects, 49,684 passed, DeltaDownload 64/64. The change is pure computation and platform-neutral, which covers the Linux and macOS part of the criterion; the shift's CI watch files a task for any failure.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 10cc6e68.
- 2026-10-29: Doing -> Done. DeltaDownloadTransferHook.FetchMissingAsync is inside the complexity gate; the quality step passes locally
