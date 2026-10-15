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
completed:
---
# BL-2284 — Make TransferPerformer write only through injected streams and transfer events, never to the runner's standard output or standard error

## Goal

`TransferPerformer` never touches the runner's standard output, standard error or `CommandLineOptions`-specific output plumbing directly: the body, header and diagnostic output reach it as injected `Stream`s, `ITransferEvents` and delegates, as libcurl's write, header and debug callbacks do.

## Context

ADR-0506, plan phase 1 step 2; follows BL-2283. Once this holds, `TransferPerformer` can move to `Curl.Engine.UnitLibrary` (BL-2291) without dragging the tool's output classes (`StandardOutput*`, `WriteGate*`, `WarningLineWrapper`, the progress meter) along. Upstream's line: libcurl calls `CURLOPT_WRITEFUNCTION`, `CURLOPT_HEADERFUNCTION` and `CURLOPT_DEBUGFUNCTION`; the tool decides where the bytes go.

Introduce a small parameter object (for example `TransferOutputs`: body stream, header stream or `null`, the events, a callback for the tool's message lines) and have the runner build it per transfer. `TransferPerformer` keeps reading `CommandLineOptions` for settings until the option-group retargets (BL-2303 onwards).

## Acceptance criteria

- [ ] `TransferPerformer` has no reference to `standardOutput`, `standardError`, `WriteGate`, `WarningLineWrapper`, `StandardOutput*`, `ParallelProgressMeter` or `ProgressBarRecorder` (a `Select-String` over `Curl.Console/TransferPerformer*.cs` for those names finds nothing).
- [ ] `TransferPerformerTests` drive a transfer with in-memory body and header streams and assert the bytes each received.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

## Log

- 2026-10-15: Created.
