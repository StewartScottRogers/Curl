---
id: BL-370
title: Fold Curl.Console's WindowsOutputFileNameSanitizer.Sanitize into Curl.Core's copy
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-370 — Fold Curl.Console's WindowsOutputFileNameSanitizer.Sanitize into Curl.Core's copy

## Goal

`Curl.Console`'s `WindowsOutputFileNameSanitizer` keeps only `SanitizeRemoteName`, built on `Curl.Core.Globbing.WindowsOutputFileNameSanitizer.Sanitize`, so one rule has one implementation.

## Context

- Since BL-240 an `-o` name is sanitized by `Curl.Core`'s copy (through `UrlGlobMatch.ResolveOutputFileName`); `Curl.Console`'s `Sanitize` is used only by its own `SanitizeRemoteName` and its tests. The two replace the same characters; Core's also skips a leading `\\?\` prefix.
- Keep the measured cases in `WindowsOutputFileNameSanitizerTests` that concern remote names; the `-o` cases are already pinned in `Curl.Core.UnitTests`.

## Acceptance criteria

- [x] `Curl.Console/WindowsOutputFileNameSanitizer.cs` has no `Sanitize` of its own and `SanitizeRemoteName` calls `Curl.Core.Globbing.WindowsOutputFileNameSanitizer.Sanitize`; its tests still pass.
- [x] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1 -IncludeIntegration` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- Core's `WindowsOutputFileNameSanitizer` is `internal`, so Console could not call it. Chose
  `<InternalsVisibleTo Include="curl" />` in `Curl.Core.UnitLibrary.csproj` (Console's
  assembly is named `curl`) over making the class public: no public API grows, and a public
  method would need an argument guard and a Core test for it, outside this task. That adds
  `Curl.Core.UnitLibrary` to `touches`; no task in Doing named it (BL-771: Curl.Networking.UnitTests;
  BL-436: FTP, Abstractions, Record-CurlExchange.ps1).
- The folded behaviour is identical for remote names: Core's copy differs only in skipping a
  leading `\\?\`, and a remote name holds no `\`.
- The `-o` `Sanitize` tests were removed from `Curl.Console.UnitTests`; the same cases are
  pinned in `Curl.Core.UnitTests/Globbing/UrlGlobTests.cs`
  (`ResolveOutputFileName_OnWindows_IsSanitizedAsCurlSanitizesIt`). The 11 measured
  `SanitizeRemoteName` cases stay.
- Measure-CodeQuality (2026-09-27): Curl.Console 100/100, 0 failing; Curl.Core.UnitLibrary
  100/100, 0 failing. One failing member in Curl.Networking.UnitLibrary, unrelated
  to this task.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Orphaned by a stopped shift: no lane worktree or branch held its work; requeued.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. Curl.Console's SanitizeRemoteName builds on Curl.Core's WindowsOutputFileNameSanitizer.Sanitize; the duplicate Sanitize is gone
