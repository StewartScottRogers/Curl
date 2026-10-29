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
completed: 2026-10-28
---
# BL-4031 — Forward ITransferEvents.ReportWarning in the Curl.Networking and Curl.Output wrappers that forward every member
## Goal

A warning a library reports through `ITransferEvents.ReportWarning` under a Curl.Networking or Curl.Output wrapper reaches the console's `WarningWritingTransferEvents` instead of the interface's default no-op.

## Context

BL-3986 added `ITransferEvents.ReportWarning` (default no-op); BL-3996 made the nine Curl.Engine wrappers forward it. BL-3996's sweep found ten more wrappers that forward every other member (`ReportTlsTrust` included) but not `ReportWarning`: the eight Curl.Networking and two Curl.Output files in `touches`. `TcpDialer`'s warning (BL-3714, CGR-185) passes through the Networking ones. Add `public void ReportWarning(string text) => inner.ReportWarning(text);` (or the wrapper's own inner name) to each, as `Curl.Engine.UnitTests/EngineTransferEventWarningTests.cs` pins for the engine.

## Acceptance criteria

- [x] Each wrapper in `touches` forwards `ReportWarning` to its inner events, shown by the two test files in `touches`.
- [x] `dotnet build` is clean, the fast tests pass, and both libraries stay at their quality gates.

## Notes

- Done directly rather than through the /feature stages: one forwarding line per wrapper, as BL-3996 did for the engine. `DohSubTransferEvents` forwards the warning unprefixed to its inner events (a warning is not an info line, so no `[DoH] ` prefix); `TraceIdsTransferEvents` sets the trace ID prefix under its lock as for every other member. `FlowScopedTransferEvents` forwards to its current flow's events.
- Verified: `dotnet build` clean, all fast test projects passed (Networking 3822, Output 871), `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary,Curl.Output.UnitLibrary`: 0 failing members. Changes are platform-neutral (no paths, OS errors or certificates), so CI on Linux and macOS is covered.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: a08703a3 66413580.
- 2026-10-28: Doing -> Done. Curl.Networking and Curl.Output wrappers forward ReportWarning to their inner events
