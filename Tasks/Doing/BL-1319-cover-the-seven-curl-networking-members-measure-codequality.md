---
id: BL-1319
title: Cover the seven Curl.Networking members Measure-CodeQuality reports below 100% branch or above complexity 10
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1319 — Cover the seven Curl.Networking members Measure-CodeQuality reports below 100% branch or above complexity 10

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports no failing member.

## Context

Found by BL-1284 on 2026-10-03, in code BL-1284 did not change: `AddressFamilyRace.DialAsync` (branch 88.89%, Cx 18), `TcpConnector.DialAndOpenThroughProxyAsync` (91.67%, Cx 12), `TcpConnector.RequestTunnelAsync` (70%), `TcpConnector.ConnectWithinTimeoutAsync` (83.33%), `ConnectTunnelVerboseLines.ReportBeforeConnect` (75%), `TcpConnector.SecureOpenedTunnelAsync` (50%), `PooledConnection.IsSharedWithAnotherTransfer` (50%).

## Acceptance criteria

- [ ] `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for every member.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
