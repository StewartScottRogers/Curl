---
id: BL-2318
title: Run Curl.Console's serial transfers through ICurlEngine and IEasyTransfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2316]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2318 — Run Curl.Console's serial transfers through ICurlEngine and IEasyTransfer

## Goal

`Curl.Console`'s serial (non-`--parallel`) transfers run through `ICurlEngine.CreateEasy()` and `IEasyTransfer.PerformAsync`, with options from `EasyOptionsFromCommandLine` and output through the write and header callbacks, instead of calling `TransferPerformer` directly; behaviour is byte for byte unchanged.

## Context

ADR-0506, plan phase 2 step 5 - the console's re-base onto the seam, the step before it moves onto `Libcurl` in phase 3. Needs BL-2316. `CurlComposition.CreateRunner` gets an `ICurlEngine` (the managed one, built by a plain constructor call; `InProcessCurl` passes its fakes through as today). `--parallel` keeps calling the engine directly until the multi implementation lands in phase 3; say so in `Curl.Console/CLAUDE.md`. Where the runner needs something the contract does not carry yet (a trace event, a progress tick), wire it through `EasyCallbackOptions` (progress/xferinfo, debug); if a contract change is needed, move this task to `Blocked` and file it, since `Curl.Api.UnitLibrary` is outside `touches`.

## Acceptance criteria

- [ ] For a serial run, `CurlCommandRunner` performs each transfer attempt through `IEasyTransfer.PerformAsync` (a test with a recording `ICurlEngine` fake shows one `PerformAsync` per attempt, `--retry` attempts included).
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [ ] `dotnet publish Curl.Console -c Release` still produces a native binary, or the Notes say why it could not be run on this machine.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
