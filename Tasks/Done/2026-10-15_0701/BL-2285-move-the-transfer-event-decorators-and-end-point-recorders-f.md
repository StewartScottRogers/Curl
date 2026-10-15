---
id: BL-2285
title: Move the transfer-event decorators and end-point recorders from Curl.Console to Curl.Engine.UnitLibrary
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-2281]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Directory.Build.props]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2285 — Move the transfer-event decorators and end-point recorders from Curl.Console to Curl.Engine.UnitLibrary

## Goal

The transfer-event decorators and end-point recorders live in `Curl.Engine.UnitLibrary` (namespace `Curl.Engine`), with their tests in `Curl.Engine.UnitTests`, and `Curl.Console` uses them from there.

## Context

ADR-0506, plan phase 1 step 3 (first move). A mechanical move: no behaviour change, no rename beyond the namespace. Files (from `Curl.Console`, as of 2026-10-15): `ClientReaderResetTraceEvents`, `ClientWriterTraceEvents`, `ConnectReplyHeadWritingEvents`, `ConnectionIdRecordingTransferEvents`, `MultiStateTraceEvents`, `ResponseWaitTimerTraceEvents`, `TlsResultRecordingTransferEvents`, `ConnectionEndPointRecorder`, `EndPointRecordingConnector`, `EndPointRecordingDatagramConnector`, `EndPointReportingProtocolHandler`, `LazyProtocolHandler`, `RoutingFtpProtocolHandler`, `GatedDiagnosticLog`, `LateBoundDiagnosticLog`.

- Add `Curl.Engine.UnitLibrary` to `Curl.Console/Curl.Console.csproj`'s references (and to `Curl.Console.UnitTests` if its remaining tests need it).
- A file that turns out to reference a tool-side type (one that stays in `Curl.Console`, see the plan's table) stays; say why in Notes and correct the plan's table.
- Types stay `internal` (the engine's `InternalsVisibleTo` covers `curl` and the test projects) unless a later task needs them public.
- If `Curl.Engine.UnitTests` needs the SSH test fakes and so references `Curl.Protocol.Ssh.UnitTests` (as `Curl.Console.UnitTests` does), extend the `CS0436` condition in `Directory.Build.props` to it.
- Move each file's paragraphs of `Curl.Console/CLAUDE.md` to `Curl.Engine.UnitLibrary/CLAUDE.md`.

## Acceptance criteria

- [x] None of the files named in Context (other than any kept with a reason in Notes) remains in `Curl.Console`; each is in `Curl.Engine.UnitLibrary` in namespace `Curl.Engine`.
- [x] Their tests are in `Curl.Engine.UnitTests` with the same test names and assertions; none was deleted.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened (the fast-test count is not lower than before).
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30), `Curl.Engine.UnitLibrary` included.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [x] `Curl.Console/CLAUDE.md` and `Curl.Engine.UnitLibrary/CLAUDE.md` each describe only the code in their own project.

## Notes

- Stayed in Curl.Console, each using a tool-side type that has not moved: ConnectionIdRecordingTransferEvents and TlsResultRecordingTransferEvents (RunningTransferState), MultiStateTraceEvents (TransferTimers, BL-2288), GatedDiagnosticLog (WriteGate). Their tests stayed too. The plan's table in Curl-Api-And-libcurl.md is corrected.
- Engine.UnitTests carries copies of the Console test fakes the moved tests use (RecordingProtocolHandler, ScriptedConnector and friends, two CurlRunDiagnostics helpers); it does not reference Ssh.UnitTests, so Directory.Build.props needed no change.
- One new test, Handler_GivenALazyHandler_IsTheBuiltHandler, keeps EndPointReportingProtocolHandler at 100% branch coverage on its own. Measure-CodeQuality for Curl.Engine.UnitLibrary: 100/100, worst CRAP 10.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Moved the event decorators and end-point recorders to Curl.Engine
