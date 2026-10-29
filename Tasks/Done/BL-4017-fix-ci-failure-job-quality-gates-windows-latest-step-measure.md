---
id: BL-4017
title: Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1, Curl.Core.UnitTests/Multipart/MultipartFormBodyBuilderMissingFileTests.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.HttpPost.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4017 — Fix CI failure job Quality gates (windows-latest) step Measure coverage, complexity and CRAP on Windows

## Goal

`job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` failed on Windows in CI run 40801399142 (https://github.com/StewartScottRogers/Curl/actions/runs/40801399142). First failing commit: fcc2b504.

    [36;1m  Write-Host "::error file=$($m.file.Replace('\', '/')),line=$($m.line)::$($m.member) is outside a gate: $($m.fails -join ', ')"[0m; MultipartFormBodyBuilder.MissingFileFailure(Curl.Core.Multipart.MultipartBodySegments, Curl.Core.Multipart.MultipartFormPart, string, Curl.Core.Multipart.MultipartNameEscaping, bool, Curl.Core.Multipart.MultipartPartEncoder) is outside a gate: branch; ManagedEasyTransfer.PerformBuildingMimePostAsync (async) is outside a gate: branch

Lanes test only on Windows, so reproduce with `gh run view 40801399142 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates (windows-latest) step Measure coverage, complexity and CRAP` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- The gate failures came from afe4338c7 (BL-3791), not fcc2b504. No test covered `MissingFileFailure`'s `encoder?.Name` when the part waits for the send and has an encoder. `PerformBuildingMimePostAsync` lost its only test of a form that fails to build once a missing file stopped failing at build. Fixed with two tests and no production change: a waiting missing file with a base64 encoder (Curl.Core.UnitTests), and a `CURLOPT_MIMEPOST` whose name holds a line feed under `CURLMIMEOPT_FORMESCAPE`, which fails with exit 43 before connecting (Curl.Engine.UnitTests). Added both test files to touches: no task in Doing on origin/work/dark-factory names them.
- `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary,Curl.Engine.UnitLibrary`: 0 failing members. It was scored with -SkipTestRun after its test run, because Libcurl.UnitTests failed three tests in a static-state race between Cgr265Tests and EasyAuthenticationOptionSettersTests (filed as BL-4023). Every other test project that reaches Core and Engine passed in that run.
- CI on Linux and macOS: both new tests are platform-neutral (an in-memory file system and a scripted connector, no path or OS text). That and the green tests here meet the criterion; the shift's CI watch files any failure.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 10cc6e68.
- 2026-10-28: Doing -> Done. Curl.Core and Curl.Engine are back inside the branch gate: two tests cover the waiting missing file with an encoder and a MIME post that fails to build
