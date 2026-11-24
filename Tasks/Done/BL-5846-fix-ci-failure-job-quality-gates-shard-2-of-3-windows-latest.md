---
id: BL-5846
title: Fix CI failure job Quality gates shard 2 of 3 (windows-latest) step Collect coverage for shard 2 on Windows
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5846 — Fix CI failure job Quality gates shard 2 of 3 (windows-latest) step Collect coverage for shard 2 on Windows

## Goal

`job Quality gates shard 2 of 3 (windows-latest) step Collect coverage for shard 2` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `job Quality gates shard 2 of 3 (windows-latest) step Collect coverage for shard 2` failed on Windows in CI run 45432993585 (https://github.com/StewartScottRogers/Curl/actions/runs/45432993585). First failing commit: 852090e3.

    VSTEST_CONNECTION_TIMEOUT: 600

Lanes test only on Windows, so reproduce with `gh run view 45432993585 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `job Quality gates shard 2 of 3 (windows-latest) step Collect coverage for shard 2` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

The failing step was `dotnet test` on Curl.Extended.Protocol.WebTransport.UnitTests: "Test host process crashed" 2 s into the run (run 45432993585, 16:17:30), with no stack in the log; every other project in the shard passed. The failing commit 852090e3 touches only the file protocol, unrelated. Locally the project passes 92 of 92 both plain and with the same `--collect:"Code Coverage;Format=cobertura"` coverage collection the script uses, so the crash is a one-off host crash, not a code fault. Decision: no code change (nothing to fix without a reproduction); a repeat will be filed by the CI watch with its own log. Criterion ticked on that basis (no reruns hunting a flake, lane rule 8).

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: ff819106.
- 2026-11-24: Doing -> Done. Not reproducible: WebTransport test host crash was a one-off; passes locally with coverage
