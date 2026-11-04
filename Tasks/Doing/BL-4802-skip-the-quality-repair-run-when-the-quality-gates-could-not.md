---
id: BL-4802
title: Skip the quality repair run when the quality gates could not be measured
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/Integrate.ps1]
requirement: none
created: 2026-11-03
completed:
---
# BL-4802 — Skip the quality repair run when the quality gates could not be measured

## Goal

When `Test-QualityGates` cannot measure the gates (git diff failed, or the report is unreadable), the lane does not start a 30-minute `Repair-QualityGates` run that no code change can satisfy.

## Context

Found by the shift-end review of 2026-11-03 (ADR-0710), by both the silent-failure-hunter and the code-reviewer, and verified against the code. BL-4785 (diff 77d5a82d9...4501875e6) made `Test-QualityGates` (`DarkFactory/Integrate.ps1:139-143`, `:162-166`) return `quality gates not measured: git diff against <base> failed` or `quality gates not measured: the report is unreadable`. The caller at `DarkFactory/Integrate.ps1:307-308` treats any non-empty result for a Done task as a gate miss. It runs `Repair-QualityGates`, a Claude run of up to 30 minutes whose prompt is "fix quality gates not measured: ...", then measures again, which most likely fails the same way, and returns "..., still after a repair run". The task is requeued and uses one of its two claims (ADR-1155). A second requeue sends good work to the bounded split.

Failing closed (not pushing unmeasured code) is right. Spending a repair run on an infrastructure failure is not.

## Acceptance criteria

- [ ] `Integrate.ps1` calls `Repair-QualityGates` only for a real gate miss (`quality gates missed ...`), never for a `quality gates not measured ...` result. That result is still returned, so the code is not pushed.
- [ ] A `-TestIntegrateLock` case shows that a not-measured result for a Done task returns the reason without calling `Repair-QualityGates` (`Invoke-TaskRun` is not run).
- [ ] The comment above the call at `Integrate.ps1:303-306` says what happens to a not-measured result.
- [ ] `RunDarkFactory.ps1 -TestIntegrateLock` passes under Windows PowerShell and `pwsh`.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: 4d7bd0b2 f6fb8d53 d9e02a2a 85027b50.
