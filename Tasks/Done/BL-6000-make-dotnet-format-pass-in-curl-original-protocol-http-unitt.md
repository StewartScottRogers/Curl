---
id: BL-6000
title: Make dotnet format pass in Curl.Original.Protocol.Http.UnitTests
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6000 — Make dotnet format pass in Curl.Original.Protocol.Http.UnitTests

## Goal

`dotnet format Curl.slnx --verify-no-changes --include <each path in touches>` exits 0: the `using` order, whitespace and final newlines in this task's touches follow `.editorconfig`.

## Context

Part of BL-5923, split by project group so lanes run the parts in parallel. On 2026-11-26 `dotnet format` reported 680 files across 53 projects: 636 IMPORTS (`using` order), 31 FINALNEWLINE and 13 WHITESPACE. Line endings are left to git (ADR-1417), so there are no ENDOFLINE fixes. Run `dotnet format Curl.slnx --include <each path in touches>`, and change nothing outside touches. It is a formatting-only change; if `dotnet format` wants to change code beyond `using` order and whitespace, leave that file alone and say why under Notes.

## Acceptance criteria

- [x] `dotnet format Curl.slnx --verify-no-changes --include <each path in touches>` exits 0.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Ran `dotnet format` on the touches: 152 files changed (152 IMPORTS, 2 WHITESPACE findings); the only changes beyond `using` lines are two closing braces split onto their own lines. `--verify-no-changes` then exits 0; build 0 warnings; fast tests 58406 passed, 0 failed, 363 skipped.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 03521b86 3d836d5e.
- 2026-11-26: Doing -> Done. dotnet format verifies clean on Curl.Original.Protocol.Http.UnitTests
