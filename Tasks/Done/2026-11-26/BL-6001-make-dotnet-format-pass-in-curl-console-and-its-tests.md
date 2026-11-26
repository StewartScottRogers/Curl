---
id: BL-6001
title: Make dotnet format pass in Curl.Console and its tests
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6001 — Make dotnet format pass in Curl.Console and its tests

## Goal

`dotnet format Curl.slnx --verify-no-changes --include <each path in touches>` exits 0: the `using` order, whitespace and final newlines in this task's touches follow `.editorconfig`.

## Context

Part of BL-5923, split by project group so lanes run the parts in parallel. On 2026-11-26 `dotnet format` reported 680 files across 53 projects: 636 IMPORTS (`using` order), 31 FINALNEWLINE and 13 WHITESPACE. Line endings are left to git (ADR-1417), so there are no ENDOFLINE fixes. Run `dotnet format Curl.slnx --include <each path in touches>`, and change nothing outside touches. It is a formatting-only change; if `dotnet format` wants to change code beyond `using` order and whitespace, leave that file alone and say why under Notes.

## Acceptance criteria

- [x] `dotnet format Curl.slnx --verify-no-changes --include <each path in touches>` exits 0.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- `dotnet format` changed 138 files, only `using` order, blank lines and a final newline; no file had to be left alone.
- Verified 2026-11-26: format check exits 0, build 0 warnings 0 errors, all 115 fast test assemblies pass, Measure-CodeQuality.ps1 -Library Curl.Console reports 0 failing members.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 03521b86 3d836d5e.
- 2026-11-26: Doing -> Done. dotnet format verifies clean for Curl.Console and Curl.Console.UnitTests
