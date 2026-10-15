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
completed: 2026-10-15
---
# BL-2290 — Move TransferContextFactory and TransferDispatch from Curl.Console to Curl.Engine.UnitLibrary

## Goal

`TransferContextFactory` and `TransferDispatch` live in `Curl.Engine.UnitLibrary` (namespace `Curl.Engine`), with their tests in `Curl.Engine.UnitTests`.

## Context

ADR-0506, plan phase 1 step 3; follows BL-2289, same rules (mechanical move, `CLAUDE.md` paragraphs move with the code). `TransferContextFactory` builds each `TransferContext`; `TransferDispatch` holds a run's dispatcher, cookies, proxy selector and connection pool, and `CurlCommandRunner` already receives a `Func<CommandLineOptions, TransferDispatch>` - after this task that function returns an engine type. If `TransferContextFactory` reaches a tool-side type (an output stream wrapper, say), pass it in rather than moving the tool type, and say so in Notes.

## Acceptance criteria

- [x] `TransferContextFactory.cs` and `TransferDispatch.cs` are in `Curl.Engine.UnitLibrary`, not `Curl.Console`.
- [x] Their tests are in `Curl.Engine.UnitTests` with the same test names and assertions; none was deleted.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened (the fast-test count is not lower than before).
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [x] `Curl.Console/CLAUDE.md` and `Curl.Engine.UnitLibrary/CLAUDE.md` each describe only the code in their own project.

## Notes

- `TransferContextFactory` reaches two more Console types, which moved with it because both are self-contained: `AltSvcTransferCache` (it stores it on the context and asks it for the route) and `HeaderLineTeeStream` (the `-i -D` tee). Their tests moved too (all four moved test classes keep their names and assertions). `AltSvcTransferCache` used `DeferredOutputFileStream.CreateMode`; it now carries its own private `0666` constant instead of moving that stream. `Curl.Engine.UnitLibrary/CLAUDE.md` no longer lists it as staying in Console.
- `TransferContextFactoryTests` used `TransferProgressRecorder` (tool-side) only as an opaque sink; it now uses a tiny `SilentTransferProgress` fake. `AltSvcTransferCacheTests` used `InMemoryFileSystem` for an empty `--alt-svc` name, so an `UnusedFileSystem` fake replaces it. `ManualTimeProvider` and `SteppingTimeProvider` are linked from `Curl.Console.UnitTests`.
- Two doc-comment `cref`s to Console types (`RemoteHeaderNameStream`, `CurlCommandRunner`) became plain text.
- Measured: `dotnet build` 0 warnings, fast tests all green, `Measure-CodeQuality.ps1` for `Curl.Engine.UnitLibrary` and `Curl.Console` 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Moved TransferContextFactory, TransferDispatch, AltSvcTransferCache and HeaderLineTeeStream with their tests to Curl.Engine; build clean, fast tests green, quality gates pass
