---
id: BL-4018
title: Move the progress meter from Curl.Console to Curl.Engine.UnitLibrary so Libcurl can draw it
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/TransferProgressRecorder.cs, Curl.Console/ProgressMeterLines.cs, Curl.Console/ProgressBarRecorder.cs, Curl.Console/ParallelTransferProgress.cs, Curl.Console/HoldableStream.cs, Curl.Console.UnitTests/TransferProgressRecorderTests.cs, Curl.Console.UnitTests/ProgressBarRecorderTests.cs, Curl.Console.UnitTests/HoldableStreamTests.cs, Curl.Console.UnitTests/ParallelTransferProgressTests.cs, Curl.Engine.UnitLibrary/TransferProgressRecorder.cs, Curl.Engine.UnitLibrary/ProgressMeterLines.cs, Curl.Engine.UnitLibrary/ProgressBarRecorder.cs, Curl.Engine.UnitLibrary/ParallelTransferProgress.cs, Curl.Engine.UnitLibrary/HoldableStream.cs, Curl.Engine.UnitTests/TransferProgressRecorderTests.cs, Curl.Engine.UnitTests/ProgressBarRecorderTests.cs, Curl.Engine.UnitTests/HoldableStreamTests.cs, Curl.Engine.UnitTests/ParallelTransferProgressTests.cs, Curl.Engine.UnitLibrary/EngineToolTransferProgress.cs, Curl.Console/CurlCommandRunner.cs, Curl.Console/RunningTransferState.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4018 — Move the progress meter from Curl.Console to Curl.Engine.UnitLibrary so Libcurl can draw it

## Goal

The built-in progress meter types (`TransferProgressRecorder`, `ProgressMeterLines`, and what they need: `ProgressBarRecorder`, `ParallelTransferProgress`, `HoldableStream`) live in Curl.Engine.UnitLibrary (namespace `Curl.Engine`), with the curl tool drawing byte-for-byte the same meter as before.

## Context

Prerequisite of BL-4012 (Libcurl draws the meter under `CURLOPT_NOPROGRESS=0`). Curl.Engine cannot reference Curl.Console, so the meter must move down. `TransferProgressRecorder` depends only on Curl.Output, Curl.Protocol.Abstractions and the three Console types named above; Curl.Engine.UnitLibrary already has `InternalsVisibleTo Include="curl"` and `Curl.Console.UnitTests`, so the types can stay `internal`. A pure move: no behaviour change. Move each type's tests with it (from Curl.Console.UnitTests to Curl.Engine.UnitTests) so Engine keeps 100% line and branch coverage from its own tests; update `using` lines in Curl.Console callers (CurlCommandRunner.cs, RunningTransferState.cs, and any other file a build error names, adding it to `touches`) and the `<c>TransferProgressRecorder</c>` cref text in EngineToolTransferProgress.cs. Other Console test files that only use the types (Cgr217Tests, CurlCommandRunner*ProgressMeterTests, CurlCompositionTests, DifferentialOutputNormaliserTests) need at most a `using Curl.Engine;`.

## Acceptance criteria

- [ ] `TransferProgressRecorder.cs`, `ProgressMeterLines.cs`, `ProgressBarRecorder.cs`, `ParallelTransferProgress.cs` and `HoldableStream.cs` are in Curl.Engine.UnitLibrary and no longer in Curl.Console.
- [ ] Their unit tests are in Curl.Engine.UnitTests and pass.
- [ ] `dotnet build` is clean and the fast tests are green.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` reports no member of the moved types outside a gate.

## Notes

- Filed by BL-4012's run (lane 11, 2026-10-28) as the move that task needs first.

## Log

- 2026-10-28: Created.
