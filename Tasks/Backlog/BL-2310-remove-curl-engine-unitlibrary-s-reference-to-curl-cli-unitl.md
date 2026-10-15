---
id: BL-2310
title: Remove Curl.Engine.UnitLibrary's reference to Curl.Cli.UnitLibrary so the engine reads only the easy option groups
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2309, BL-2323, BL-2324]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2310 — Remove Curl.Engine.UnitLibrary's reference to Curl.Cli.UnitLibrary so the engine reads only the easy option groups

## Goal

`Curl.Engine.UnitLibrary` no longer references `Curl.Cli.UnitLibrary`, so the build itself proves the engine is free of the command line.

## Context

ADR-0506; plan phase 1 step 4 (closing step); follows BL-2309, after which no engine source names `CommandLineOptions`. Remove the `ProjectReference`; fix whatever else from `Curl.Cli` the engine still used (an enum or a parsed-value type) by passing a value in or defining the engine's own type; if the clean answer is moving a type to `Curl.Core` or a seam folder (outside this task's `touches`), move this task to `Blocked` and file that move as its dependency, per the task-board skill. `Curl.Engine.UnitTests` may keep referencing `Curl.Cli` only if a test needs to build a command line; prefer not.

## Acceptance criteria

- [ ] `Curl.Engine.UnitLibrary/Curl.Engine.UnitLibrary.csproj` has no reference to `Curl.Cli.UnitLibrary`, and no engine source has `using Curl.Cli;`.
- [ ] `Curl.Engine.UnitLibrary/CLAUDE.md` states that the engine must never reference `Curl.Cli`.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [ ] `dotnet publish Curl.Console -c Release` still produces a native binary, or the Notes say why it could not be run on this machine.

## Notes

## Log

- 2026-10-15: Created.
