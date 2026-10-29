---
id: BL-4031
title: Forward ITransferEvents.ReportWarning in the Curl.Networking and Curl.Output wrappers that forward every member
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/AsyncResolveTeardownTraceEvents.cs, Curl.Networking.UnitLibrary/ConnectAttemptTraceEvents.cs, Curl.Networking.UnitLibrary/DnsFilterTraceEvents.cs, Curl.Networking.UnitLibrary/FlowScopedTransferEvents.cs, Curl.Networking.UnitLibrary/HandshakeCapturingTransferEvents.cs, Curl.Networking.UnitLibrary/HttpsConnectFilterTraceEvents.cs, Curl.Networking.UnitLibrary/SetupFilterTraceEvents.cs, Curl.Networking.UnitLibrary/SspiFailureRecordingTransferEvents.cs, Curl.Output.UnitLibrary/DohSubTransferEvents.cs, Curl.Output.UnitLibrary/TraceIdsTransferEvents.cs, Curl.Networking.UnitTests/NetworkingTransferEventWarningTests.cs, Curl.Output.UnitTests/OutputTransferEventWarningTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4031 — Forward ITransferEvents.ReportWarning in the Curl.Networking and Curl.Output wrappers that forward every member
## Goal

A warning a library reports through `ITransferEvents.ReportWarning` under a Curl.Networking or Curl.Output wrapper reaches the console's `WarningWritingTransferEvents` instead of the interface's default no-op.

## Context

BL-3986 added `ITransferEvents.ReportWarning` (default no-op); BL-3996 made the nine Curl.Engine wrappers forward it. BL-3996's sweep found ten more wrappers that forward every other member (`ReportTlsTrust` included) but not `ReportWarning`: the eight Curl.Networking and two Curl.Output files in `touches`. `TcpDialer`'s warning (BL-3714, CGR-185) passes through the Networking ones. Add `public void ReportWarning(string text) => inner.ReportWarning(text);` (or the wrapper's own inner name) to each, as `Curl.Engine.UnitTests/EngineTransferEventWarningTests.cs` pins for the engine.

## Acceptance criteria

- [ ] Each wrapper in `touches` forwards `ReportWarning` to its inner events, shown by the two test files in `touches`.
- [ ] `dotnet build` is clean, the fast tests pass, and both libraries stay at their quality gates.

## Notes

## Log

- 2026-10-28: Created.
