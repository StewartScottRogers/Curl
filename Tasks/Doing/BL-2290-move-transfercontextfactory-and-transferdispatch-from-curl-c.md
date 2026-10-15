---
id: BL-2290
title: Move TransferContextFactory and TransferDispatch from Curl.Console to Curl.Engine.UnitLibrary
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-2289]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2290 — Move TransferContextFactory and TransferDispatch from Curl.Console to Curl.Engine.UnitLibrary

## Goal

`TransferContextFactory` and `TransferDispatch` live in `Curl.Engine.UnitLibrary` (namespace `Curl.Engine`), with their tests in `Curl.Engine.UnitTests`.

## Context

ADR-0506, plan phase 1 step 3; follows BL-2289, same rules (mechanical move, `CLAUDE.md` paragraphs move with the code). `TransferContextFactory` builds each `TransferContext`; `TransferDispatch` holds a run's dispatcher, cookies, proxy selector and connection pool, and `CurlCommandRunner` already receives a `Func<CommandLineOptions, TransferDispatch>` - after this task that function returns an engine type. If `TransferContextFactory` reaches a tool-side type (an output stream wrapper, say), pass it in rather than moving the tool type, and say so in Notes.

## Acceptance criteria

- [ ] `TransferContextFactory.cs` and `TransferDispatch.cs` are in `Curl.Engine.UnitLibrary`, not `Curl.Console`.
- [ ] Their tests are in `Curl.Engine.UnitTests` with the same test names and assertions; none was deleted.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened (the fast-test count is not lower than before).
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [ ] `Curl.Console/CLAUDE.md` and `Curl.Engine.UnitLibrary/CLAUDE.md` each describe only the code in their own project.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
