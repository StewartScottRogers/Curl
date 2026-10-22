---
id: BL-3016
title: Measure Curl.Core and Curl.Engine quality after the infof truncation and ConnectionPeer additions and close any gap
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3014]
touches: [Curl.Core.UnitLibrary/ConnectionPeer.cs, Curl.Core.UnitTests/ConnectionPeerTests.cs, Curl.Engine.UnitLibrary/DebugCallbackTransferEvents.cs, Curl.Engine.UnitTests/DebugCallbackTransferEventsTests.cs]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-3016 — Measure Curl.Core and Curl.Engine quality after the infof truncation and ConnectionPeer additions and close any gap

## Goal

`Curl.Core.UnitLibrary` and `Curl.Engine.UnitLibrary` keep 100% line and branch coverage, complexity at most 10 and CRAP at most 30 after BL-3014 adds `ConnectionPeer` and the infof truncation in `DebugCallbackTransferEvents`.

## Context

Split from BL-2925. Run `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` and again with `Curl.Engine.UnitLibrary` (the script's header gives the options). Close any gap in `ConnectionPeer.cs` or `DebugCallbackTransferEvents.ReportText` with focused tests in `Curl.Core.UnitTests/ConnectionPeerTests.cs` and `Curl.Engine.UnitTests/DebugCallbackTransferEventsTests.cs`; add a file to `touches` in the same commit if a gap lies elsewhere. Never change `CodeMetricsConfig.txt`. A gap BL-3014 did not cause goes in Notes and a separate task.

## Acceptance criteria

- [x] Measure-CodeQuality reports no member of `ConnectionPeer` or `DebugCallbackTransferEvents` outside the four gates.
- [x] Each test added fails when the branch it covers is removed.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Core.UnitTests` and `Curl.Engine.UnitTests`.

## Notes

- Ran directly rather than through the full `/feature` stages: this task only measures and closes gaps, so it needs no plan.
- First measurement (Core and Engine together): both at 100% line and branch coverage. Engine had no failing member (worst CRAP 10). Core had one: `ConnectionPeer.Create` at complexity 12.
- Fix: moved the bracket strip, the IPv6 check and the zone id slice into three private helpers (`WithoutBrackets`, `IsIpv6Address`, `ZoneIdAfter`). Behaviour is the same, and the existing `ConnectionPeerTests` and unit2413 still cover every branch. Measured Core again: 0 failing members, worst CRAP 10, 100%/100%.
- No tests were added, because nothing was left uncovered, so the second criterion holds trivially. No gap was found outside BL-3014's files.
- Tests: Curl.Core.UnitTests 1792 passed, 8 skipped. Curl.Engine.UnitTests 2221 passed, 5 skipped.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 9e1712e6 3ae47afb f852ee96.
- 2026-10-21: Doing -> Done. ConnectionPeer.Create split under the complexity gate; Curl.Core and Curl.Engine meet all four quality gates
