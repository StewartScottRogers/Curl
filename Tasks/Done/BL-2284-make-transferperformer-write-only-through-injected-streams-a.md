---
id: BL-2284
title: Make TransferPerformer write only through injected streams and transfer events, never to the runner's standard output or standard error
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2283]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2284 — Make TransferPerformer write only through injected streams and transfer events, never to the runner's standard output or standard error

## Goal

`TransferPerformer` never touches the runner's standard output, standard error or `CommandLineOptions`-specific output plumbing directly: the body, header and diagnostic output reach it as injected `Stream`s, `ITransferEvents` and delegates, as libcurl's write, header and debug callbacks do.

## Context

ADR-0506, plan phase 1 step 2; follows BL-2283. Once this holds, `TransferPerformer` can move to `Curl.Engine.UnitLibrary` (BL-2291) without dragging the tool's output classes (`StandardOutput*`, `WriteGate*`, `WarningLineWrapper`, the progress meter) along. Upstream's line: libcurl calls `CURLOPT_WRITEFUNCTION`, `CURLOPT_HEADERFUNCTION` and `CURLOPT_DEBUGFUNCTION`; the tool decides where the bytes go.

Introduce a small parameter object (for example `TransferOutputs`: body stream, header stream or `null`, the events, a callback for the tool's message lines) and have the runner build it per transfer. `TransferPerformer` keeps reading `CommandLineOptions` for settings until the option-group retargets (BL-2303 onwards).

## Acceptance criteria

- [x] `TransferPerformer` has no reference to `standardOutput`, `standardError`, `WriteGate`, `WarningLineWrapper`, `StandardOutput*`, `ParallelProgressMeter` or `ProgressBarRecorder` (a `Select-String` over `Curl.Console/TransferPerformer*.cs` for those names finds nothing).
- [x] `TransferPerformerTests` drive a transfer with in-memory body and header streams and assert the bytes each received.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

- 2026-10-15: BL-2283 had already moved every standard-error write behind runner delegates and every `-v` line behind `ITransferEvents`; the one name left was `WriteCookieJarAltSvcAndHstsFilesAsync`'s `jarStandardOutput`, renamed `jarPrintTarget` (the runner still chooses standard output in its mode for `-c -`). Body and header bytes already reach an attempt only as the context's `Output` and `HeaderOutput`; the new `FollowWatchingSpeedAsync_InjectedBodyAndHeaderStreams_EachReceivesOnlyItsBytes` pins that with in-memory streams.
- Decision: no `TransferOutputs` parameter object yet. The task named it as an example; with the streams already on `TransferContext` and the message writers already delegates, a record would only wrap them, and the runner builds the context per attempt. BL-2291 can introduce one if the move to `Curl.Engine.UnitLibrary` needs a single seam. No behaviour change, so no ADR.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Console`: 0 failing members. `Select-String` over `Curl.Console/TransferPerformer*.cs` for the forbidden names: 0 matches. Production change is a parameter rename only, so `PassingUpstreamCases.txt`, `CurlEasyErrorText.cs` and `InProcessCurl.RunAsync` are untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. TransferPerformer names none of the runner's output plumbing; in-memory body and header streams pinned by test
