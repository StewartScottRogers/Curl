---
id: BL-003
title: Scaffold the Phase 1 projects
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
requirement: none
created: 2026-09-25
completed: 2026-09-26
---
# BL-003 — Scaffold the Phase 1 projects

## Goal

Every Phase 1 library and its `.UnitTests` twin exists, is listed in `Curl.slnx`,
and builds.

## Context

Phase 1 is `Abstractions`, `Networking`, `Core`, `Cli`, `Output`, `Console`, `File`
and `Http`, each with its `.UnitTests` project. Scaffolding them also clears the
`NU1503 Unable to find a project to restore!` warning that `Roadmap.md` records for
an empty solution. Use the `new-project` skill for anything missing.

## Acceptance criteria

- [x] All sixteen Phase 1 projects exist as directories under the repository root
      and are listed in `Curl.slnx`.
- [x] `dotnet build Curl.slnx -warnaserror` succeeds with no `NU1503`.
- [x] `dotnet test --filter "TestCategory!=Integration"` runs green.

## Notes

Migration note: when this task was migrated, all sixteen projects were already
listed in `Curl.slnx` (commit 4d26d1e, "Scaffold Curl solution architecture"). This
task may only need verifying and closing. If so, also update the `Outstanding` line
of Milestone 0 in `Documentation/Planning/Roadmap.md`.

2026-09-26 verification (dark-factory run): confirmed only verifying was needed; no
project was scaffolded. All sixteen Phase 1 directories exist and are in `Curl.slnx`
(plus the other protocol projects). `dotnet build Curl.slnx -warnaserror`: 0 warnings,
0 errors, no `NU1503`. Fast tests: exit 0, 153 passed (121 File, 32 Abstractions); the
other test projects hold no tests yet and report "No test matches", which is not a
failure. Roadmap Milestone 0 `Outstanding` line updated to record the fix.

## Log

- 2026-09-25: Migrated from Documentation/Planning/Backlog.md (Ready).
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. All sixteen Phase 1 projects build clean with -warnaserror, no NU1503; fast tests green
