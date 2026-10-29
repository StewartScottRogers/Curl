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
completed:
---
# BL-3996 — Forward ITransferEvents.ReportWarning in every Curl.Engine wrapper that forwards every member

## Goal

A warning a library reports through `ITransferEvents.ReportWarning` under the engine gets to the console's `WarningWritingTransferEvents` and is not dropped by an engine wrapper's default no-op.

## Context

BL-3986 added `ITransferEvents.ReportWarning` (default no-op) and the console writer. The nine `Curl.Engine.UnitLibrary` wrappers in `touches` forward every other member (`ReportTlsTrust` included), so without a forward they swallow the warning BL-3714 (CGR-185) reports from `TcpDialer`. Add `public void ReportWarning(string text) => inner.ReportWarning(text);` to each and check `Libcurl` for any wrapper that forwards every member.

## Acceptance criteria

- [ ] Each wrapper in `touches` forwards `ReportWarning` to its inner events, shown by `Curl.Engine.UnitTests/EngineTransferEventWarningTests.cs`.
- [ ] `dotnet build` is clean, the fast tests pass, and Curl.Engine.UnitLibrary stays at its quality gates.

## Notes

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 39f1d285 a7ebedc8.
