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
completed:
---
# BL-3016 — Measure Curl.Core and Curl.Engine quality after the infof truncation and ConnectionPeer additions and close any gap

## Goal

`Curl.Core.UnitLibrary` and `Curl.Engine.UnitLibrary` keep 100% line and branch coverage, complexity at most 10 and CRAP at most 30 after BL-3014 adds `ConnectionPeer` and the infof truncation in `DebugCallbackTransferEvents`.

## Context

Split from BL-2925. Run `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` and again with `Curl.Engine.UnitLibrary` (the script's header gives the options). Close any gap in `ConnectionPeer.cs` or `DebugCallbackTransferEvents.ReportText` with focused tests in `Curl.Core.UnitTests/ConnectionPeerTests.cs` and `Curl.Engine.UnitTests/DebugCallbackTransferEventsTests.cs`; add a file to `touches` in the same commit if a gap lies elsewhere. Never change `CodeMetricsConfig.txt`. A gap BL-3014 did not cause goes in Notes and a separate task.

## Acceptance criteria

- [ ] Measure-CodeQuality reports no member of `ConnectionPeer` or `DebugCallbackTransferEvents` outside the four gates.
- [ ] Each test added fails when the branch it covers is removed.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Core.UnitTests` and `Curl.Engine.UnitTests`.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: 9e1712e6 3ae47afb f852ee96.
