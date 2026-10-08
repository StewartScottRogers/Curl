---
id: BL-1607
title: Have Find-WeakTests.ps1 scan IntegrationTests projects as well as UnitTests projects
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1597]
touches: [Audit/Tools/Find-WeakTests.ps1]
lane: no
requirement: none
created: 2026-10-07
completed:
---
# BL-1607 — Have Find-WeakTests.ps1 scan IntegrationTests projects as well as UnitTests projects

## Goal

`audit-quality`'s weak-test scan covers every test project: `Audit/Tools/Find-WeakTests.ps1` lists candidate weak tests from `*.IntegrationTests` projects as well as `*.UnitTests` ones, so moving a test into an IntegrationTests project never hides it from the audit.

## Context

- Rule (Stewart, 2026-10-07; ADR by BL-1598): Integration tests live only in `Curl.<Area>.IntegrationTests` projects. `Curl.Networking.IntegrationTests` exists (BL-1597); Cli, Console, Core and SSH follow (BL-1599 to BL-1602).
- `Find-WeakTests.ps1`'s `Invoke-Scan` (around line 146, 2026-10-07) enumerates only `Get-ChildItem -Directory -Filter '*.UnitTests'`, and its help (lines 3-9) says "every *.UnitTests project". Scan both suffixes and say so in the help.
- `-SelfTest` (around line 160) builds a sample tree with `Sample.UnitTests/SampleTests.cs`; add a `Sample.IntegrationTests` file with one weak test and check it is listed.
- **Interactive only.** `Audit/` is an audit path (ADR-0267): this task carries `lane: no`, the change goes through the `audit` branch, and an interactive session merges its pull request once CI is green (root `CLAUDE.md`, "Git and GitHub"). A factory lane must not claim it.
- `Audit/Tools/Invoke-MutationTest.ps1` runs a library's `.UnitTests` twin, which is right: mutation scores are measured by the fast tests. Leave it unchanged and say so in Notes.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Audit/Tools/Find-WeakTests.ps1 -SelfTest` passes, including the new IntegrationTests case.
- [ ] Run against the repository, the scan's project count includes every `*.IntegrationTests` folder; Notes record the before and after project and test counts.
- [ ] The script's help says it scans `*.UnitTests` and `*.IntegrationTests` projects.
- [ ] The change is committed on the `audit` branch, not `work/dark-factory`.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Blocked. Done on the audit branch; waiting on PR #72's CI before merging to master
