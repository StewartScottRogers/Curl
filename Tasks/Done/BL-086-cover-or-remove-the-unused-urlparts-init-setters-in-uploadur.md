---
id: BL-086
title: Cover or remove the unused UrlParts init setters in UploadUrl
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-086 — Cover or remove the unused UrlParts init setters in UploadUrl

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line coverage, with no failing member.

## Context

- Found during BL-067 (2026-09-26): the audit reports `UrlParts.set_PathStart`,
  `set_QueryStart` and `set_FragmentStart` at 0% line coverage,
  `Curl.Cli.UnitLibrary/UploadUrl.cs:85`. They are the compiler-generated `init` setters of
  the positional `private readonly record struct UrlParts`, which nothing calls.
- Likely fix: make `UrlParts` a plain `readonly struct` (or a record struct with get-only
  properties set by a constructor), so no uncallable setter is emitted. No behaviour change.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports line 100% and branch 100% with 0 failing members.
- [x] `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green with the `UploadUrl` tests unchanged.

## Notes

- `UrlParts` is now a plain `private readonly struct` with a primary constructor and get-only
  properties, so no `init` setters (or record members) are emitted. No behaviour change; the
  `UploadUrl` tests are unchanged and green.
- Choice (unattended run): after that fix the audit still failed one member, the
  `ConsolePasswordPrompt.ForProcessConsole` key-reading lambda (`ConsolePasswordPrompt.cs:18`,
  added by BL-058). It is inside this task's `touches` and the Goal needs 0 failing members, so
  it is covered here by `ForProcessConsole_InputRedirected_ReturnsAnEmptyPassword`: under
  `dotnet test` standard input is redirected, so `Console.ReadKey` throws
  `InvalidOperationException` and the prompt returns an empty password. When standard input is a
  real console the test reports Inconclusive rather than wait for a keypress.
- Result: `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports line 100%, branch 100%,
  186 members, 0 failing, worst CRAP 10. Fast tests green (Curl.Cli.UnitTests 531 passed).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Curl.Cli.UnitLibrary measures 100% line and branch coverage with 0 failing members
