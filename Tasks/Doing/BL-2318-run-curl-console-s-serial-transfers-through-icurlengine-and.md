---
id: BL-2318
title: Run Curl.Console's serial transfers through ICurlEngine and IEasyTransfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2316, BL-2317, BL-2359, BL-2360]
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

- 2026-10-15 (lane 1, prerequisite pass before any code): the serial attempt in `CurlCommandRunner.FollowRetryingAsync` runs on a context the runner builds (diagnostic log, `ITransferEvents` for `-v`/`--trace`, progress sink, `-D` tee, watchdogs on the shared `RunningTransferState`, the option group's `TransferDispatch` with its cookies and connection pool across URLs) and reads the whole `TransferReport` for `-w`. `ManagedEasyTransfer.PerformAsync` builds its own context with libcurl defaults (`TransferSettingsOf`: silent, no fail mode, no `--proto`), `Stream.Null` standard input, no events or progress, a fresh `EngineTransferState` per perform, and fills `Info` with only the URL and response code. Routing the runner through it byte for byte needs, all outside this task's `touches`:
  - BL-2317 (Backlog): `Info` filled from the report, so `-w` and the runner's report-driven output still have their values.
  - BL-2359 (filed): the tool's settings, the upload read callback, `-C`, curl's easy error text and engine-wide shared state (cookies, HSTS, alt-svc, ID numbering, connection pool) carried to `ManagedEasyTransfer` (touches `Curl.Api.UnitLibrary`).
  - BL-2360 (filed): trace events through the debug callback and progress through xferinfo, for `-v`, `--trace` and the meter.
  Requeued with those in `depends-on`; no code was changed. A console-side `IEasyTransfer` that only wrapped `TransferPerformer` was rejected: it would satisfy the recording-fake test while changing nothing, and the task says `CreateRunner` gets the managed engine.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Backlog. Needs BL-2317, BL-2359 and BL-2360: ManagedEasyTransfer cannot yet run the tool's attempt byte for byte (tool settings, shared state, trace and progress callbacks, full TransferInfo)
- 2026-10-15: Backlog -> Doing.
