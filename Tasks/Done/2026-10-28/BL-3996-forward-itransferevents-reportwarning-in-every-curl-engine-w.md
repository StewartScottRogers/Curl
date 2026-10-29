---
id: BL-3996
title: Forward ITransferEvents.ReportWarning in every Curl.Engine wrapper that forwards every member
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-3986]
touches: [Curl.Engine.UnitLibrary/AbortedHeadRecordingTransferEvents.cs, Curl.Engine.UnitLibrary/EngineConnectionIdTransferEvents.cs, Curl.Engine.UnitLibrary/TlsVerifyResultRecordingTransferEvents.cs, Curl.Engine.UnitLibrary/ResponseWaitTimerTraceEvents.cs, Curl.Engine.UnitLibrary/NegotiateFailureRecordingTransferEvents.cs, Curl.Engine.UnitLibrary/DebugCallbackTransferEvents.cs, Curl.Engine.UnitLibrary/ClientReaderResetTraceEvents.cs, Curl.Engine.UnitLibrary/ClientWriterTraceEvents.cs, Curl.Engine.UnitLibrary/ConnectReplyHeadWritingEvents.cs, Curl.Engine.UnitTests/EngineTransferEventWarningTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3996 — Forward ITransferEvents.ReportWarning in every Curl.Engine wrapper that forwards every member

## Goal

A warning a library reports through `ITransferEvents.ReportWarning` under the engine gets to the console's `WarningWritingTransferEvents` and is not dropped by an engine wrapper's default no-op.

## Context

BL-3986 added `ITransferEvents.ReportWarning` (default no-op) and the console writer. The nine `Curl.Engine.UnitLibrary` wrappers in `touches` forward every other member (`ReportTlsTrust` included), so without a forward they swallow the warning BL-3714 (CGR-185) reports from `TcpDialer`. Add `public void ReportWarning(string text) => inner.ReportWarning(text);` to each and check `Libcurl` for any wrapper that forwards every member.

## Acceptance criteria

- [x] Each wrapper in `touches` forwards `ReportWarning` to its inner events, shown by `Curl.Engine.UnitTests/EngineTransferEventWarningTests.cs`.
- [x] `dotnet build` is clean, the fast tests pass, and Curl.Engine.UnitLibrary stays at its quality gates.

## Notes

- Eight wrappers forward `ReportWarning` to their inner events. `DebugCallbackTransferEvents` has no inner events, so it hands the warning to the debug callback as one `CURLINFO_TEXT` line, the way libcurl's own infof text reaches that callback (ADR-0974).
- Libcurl has no wrapper that forwards every member. The sweep found ten wrappers in Curl.Networking and Curl.Output that drop the warning. They are outside this task's touches, so I filed them as BL-4031.
- Measure-CodeQuality for Curl.Engine.UnitLibrary: line coverage is 100%, and every member of the files this task touched is inside the gates. Five members of ManagedMultiTransfer.cs fail complexity or branch coverage. They come from BL-4016, this task did not touch them, and BL-4030 on the shared branch covers them.
- The new tests make in-memory calls only, so they do not depend on the platform. With the fast tests green here, that meets the Linux and macOS CI criterion.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 39f1d285 a7ebedc8.
- 2026-10-28: Doing -> Done. Every Curl.Engine wrapper forwards ReportWarning, and the debug callback gets it as an info line
