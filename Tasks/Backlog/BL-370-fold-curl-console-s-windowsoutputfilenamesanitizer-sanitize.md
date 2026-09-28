---
id: BL-370
title: Fold Curl.Console's WindowsOutputFileNameSanitizer.Sanitize into Curl.Core's copy
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-370 — Fold Curl.Console's WindowsOutputFileNameSanitizer.Sanitize into Curl.Core's copy

## Goal

`Curl.Console`'s `WindowsOutputFileNameSanitizer` keeps only `SanitizeRemoteName`, built on `Curl.Core.Globbing.WindowsOutputFileNameSanitizer.Sanitize`, so one rule has one implementation.

## Context

- Since BL-240 an `-o` name is sanitized by `Curl.Core`'s copy (through `UrlGlobMatch.ResolveOutputFileName`); `Curl.Console`'s `Sanitize` is used only by its own `SanitizeRemoteName` and its tests. The two replace the same characters; Core's also skips a leading `\\?\` prefix.
- Keep the measured cases in `WindowsOutputFileNameSanitizerTests` that concern remote names; the `-o` cases are already pinned in `Curl.Core.UnitTests`.

## Acceptance criteria

- [ ] `Curl.Console/WindowsOutputFileNameSanitizer.cs` has no `Sanitize` of its own and `SanitizeRemoteName` calls `Curl.Core.Globbing.WindowsOutputFileNameSanitizer.Sanitize`; its tests still pass.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -IncludeIntegration` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Orphaned by a stopped shift: no lane worktree or branch held its work; requeued.
