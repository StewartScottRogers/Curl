---
id: BL-4048
title: Cgr036Tests ConnectAsync_OpenSocketSupplies and ClosedPort_CallsOpen fail in the full fast run and pass alone
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitTests/Cgr036Tests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4048 — Cgr036Tests ConnectAsync_OpenSocketSupplies and ClosedPort_CallsOpen fail in the full fast run and pass alone

## Goal

`Cgr036Tests.ConnectAsync_OpenSocketSuppliesAConnectedSocket_UsesItAndNotTheTargetsAddress` and `ConnectAsync_ClosedPort_CallsOpenWithTheAddressThenCloseOnceAndFailsWithCouldntConnect` pass in the full fast run as they do alone.

## Context

Seen by BL-4010's run on 2026-10-28 (Windows, nine lanes building): both failed after ~106 ms in `dotnet test --filter "TestCategory!=Integration"` and passed when run alone. They likely open a real loopback socket or wait on a timing that a loaded machine misses; if a socket opens, the test belongs in an IntegrationTests project, otherwise make it wait for the event it needs.

## Acceptance criteria

- [x] Both tests wait for the event they need (or move to an Integration project if they touch a real socket), and pass in the full fast run.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Not reproducible here (passes alone and in the full Engine run). Tests run method-level parallel on a machine with nine lanes, so the freed port the closed-port test dials could be taken by a listener elsewhere, making the connect succeed. ClosedLoopbackPort now probes the port and picks another if anything accepts on it; the supplied-socket test starts AcceptAsync before the connect and bounds the wait at 30 s. No real-socket move needed (loopback only, per the lane rules).

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: cb8ac472 3d836d5e.
- 2026-10-28: Doing -> Done. Cgr036 closed-port test probes for a taken port; supplied-socket test accepts before connecting
