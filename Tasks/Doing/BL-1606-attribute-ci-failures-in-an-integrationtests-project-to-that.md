---
id: BL-1606
title: Attribute CI failures in an IntegrationTests project to that project and its library in RunDarkFactory.ps1
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-1597]
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-07
completed:
---
# BL-1606 — Attribute CI failures in an IntegrationTests project to that project and its library in RunDarkFactory.ps1

## Goal

When the coordinator files a "Fix CI failure ..." task (BL-987) for a failure in a `Curl.<Area>.IntegrationTests` project, the task names that project and its `touches` are the IntegrationTests project and `Curl.<Area>.UnitLibrary`, as it already does for `*.UnitTests`.

## Context

- Rule (Stewart, 2026-10-07; ADR by BL-1598): Integration tests live only in `Curl.<Area>.IntegrationTests` projects. `Curl.Networking.IntegrationTests` exists (BL-1597); Cli, Console, Core and SSH follow (BL-1599 to BL-1602). The `CI` workflow builds them on three platforms, so a build error in one reaches the coordinator's CI watch, and a test failure can too if a test without the Integration tag ever lands there.
- In `RunDarkFactory.ps1` (line numbers as of 2026-10-07):
  - The CI log parser (around line 2279) recognises a project in a stack trace only by `[/\\](Curl[\w.]*\.UnitTests)[/\\]`; extend it to `*.IntegrationTests`.
  - `Get-CiTouches` (around line 2357) maps `X.UnitTests` to `X.UnitTests`, `X.UnitLibrary` and `X`, and `X.UnitLibrary` to its UnitTests twin; anything else falls to `"$Project.UnitTests"`. Add `X.IntegrationTests` mapping to `X.IntegrationTests`, `X.UnitLibrary` and `X` (the last covers `Curl.Console.IntegrationTests` → `Curl.Console`). Its existing `Test-Path` filter keeps only folders that exist.
- Self-tests: the `-TestCiWatch` checks (around line 2678: `& $check 'touches' ...`) pin `Get-CiTouches` and the parser; add one case per change, e.g. `Get-CiTouches -Project 'Curl.Networking.IntegrationTests'` gives `Curl.Networking.IntegrationTests,Curl.Networking.UnitLibrary`, and a stack-trace line under `/Curl.Networking.IntegrationTests/` is attributed to that project.
- A change to `RunDarkFactory.ps1` makes an audit due (root `CLAUDE.md`, "Audit office", cadence); `Audit\Tools\Test-AuditDue.ps1` will say so, which is expected.

## Acceptance criteria

- [ ] `powershell -NoProfile -File RunDarkFactory.ps1 -TestCiWatch` passes, including the new IntegrationTests cases for `Get-CiTouches` and the log parser.
- [ ] `Get-CiTouches` and the parser's comments say they handle both test-project suffixes.
- [ ] No other behaviour of `RunDarkFactory.ps1` changes: every other `-Test*` switch the script's header lists still passes (Notes list those run).

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
