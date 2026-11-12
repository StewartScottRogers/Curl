---
id: BL-5056
title: Run Curl.Conformance.Extended.UnitTests in a lane whose commits change an extended protocol or its requirement matrix
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [DarkFactory/AffectedTests.ps1]
requirement: none
created: 2026-11-12
completed:
---
# BL-5056 — Run Curl.Conformance.Extended.UnitTests in a lane whose commits change an extended protocol or its requirement matrix

## Goal

A lane whose commits change a requirement matrix (`Documentation/Conformance/Extended/*.md`) or an extended protocol runs `Curl.Conformance.Extended.UnitTests` in its own run and verify. A matrix that disagrees with the tests is then caught before the lane takes the integrate lock or reaches CI.

## Context

`EveryMatrixAgreesWithTheTestsThatCarrySpecRequirement` (`Curl.Conformance.Extended.UnitTests/Harness/RequirementMatrixTests.cs`) checks every matrix in `Documentation/Conformance/Extended/` against the tests carrying `[SpecRequirement]`. On 2026-11-12, in shift `lanes-20261112-130616`, it broke four times:
- CI failures were filed as BL-5051 and BL-5052, and a flaky failure as BL-5054.
- BL-5013's integration hit it at 14:48 and BL-5017's at 15:35. Each time the lane ran a whole repair session while holding the integrate lock (`DarkFactory/Integrate.ps1:350`), and every other lane waited.

`Get-AffectedTestProjects` (`DarkFactory/AffectedTests.ps1:80`) picks the test projects a lane runs from the projects its commits change. `Documentation/` is no project, so a matrix-only change likely selects nothing. That is a hypothesis: confirm it first from the commits that broke the test (c7870da8, 52dd5898, 8725c8ec, 58ae04df, 0210afd2; `gh run view 43311707285 --log-failed`). If the cause is elsewhere, for example a lane changing an extended protocol's tests without the project being selected, fix that cause instead and say which under Notes.

## Acceptance criteria

- [ ] What let these commits reach the shared branch without the lane running the failing test is named under Notes, with the commit that shows it.
- [ ] `Get-AffectedTestProjects` selects `Curl.Conformance.Extended.UnitTests` when the commits change a file under `Documentation/Conformance/Extended/`: `RunDarkFactory.ps1 -TestAffectedTests` gains a case that proves it.
- [ ] `Get-AffectedTestProjects` selects `Curl.Conformance.Extended.UnitTests` when the commits change any `Curl.Extended.Protocol.*` project: a `-TestAffectedTests` case proves it.
- [ ] `powershell -NoProfile -ExecutionPolicy Bypass -File RunDarkFactory.ps1 -TestAffectedTests` passes, as does the same command under `pwsh`.

## Notes

## Log

- 2026-11-12: Created.
