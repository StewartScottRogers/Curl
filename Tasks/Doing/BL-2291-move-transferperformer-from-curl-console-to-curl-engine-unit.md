---
id: BL-2291
title: Move TransferPerformer from Curl.Console to Curl.Engine.UnitLibrary so Curl.Console holds only the curl tool's work
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2290, BL-2284]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2291 — Move TransferPerformer from Curl.Console to Curl.Engine.UnitLibrary so Curl.Console holds only the curl tool's work

## Goal

`TransferPerformer` (built by BL-2282 to BL-2284) lives in `Curl.Engine.UnitLibrary`, so every piece of libcurl's work is in the engine and `Curl.Console` holds only the curl tool's work.

## Context

ADR-0506, plan phase 1 step 3 (last move); needs BL-2290 (the dispatch and context factory are already in the engine) and BL-2284 (the performer writes only through injected outputs). After this, the plan's "stays in `Curl.Console`" column should describe `Curl.Console` exactly; correct the plan's table if it does not. The engine still reads `CommandLineOptions` (until BL-2310).

## Acceptance criteria

- [ ] `TransferPerformer` (and any class it owns) is in `Curl.Engine.UnitLibrary`; `Curl.Console` has no file defining it.
- [ ] `TransferPerformerTests` are in `Curl.Engine.UnitTests` with the same names and assertions.
- [ ] Every remaining `.cs` file in `Curl.Console` is in the plan's "stays in `Curl.Console`" column, or the plan's table is corrected in this task with the reason.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened (the fast-test count is not lower than before).
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [ ] `dotnet publish Curl.Console -c Release` still produces a native binary (vswhere on PATH, see root `CLAUDE.md`), or the Notes say why it could not be run on this machine.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
