---
id: BL-4007
title: Fix the upstream test582 SFTP multi upload hang under the socket-gated wait and run the quality gates
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890, BL-4012, BL-4013]
touches: [Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/TransferSocketWait.cs, Curl.Engine.UnitTests/Cgr034Tests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4007 — Fix the upstream test582 SFTP multi upload hang under the socket-gated wait and run the quality gates

## Goal

Upstream test582 (`Lib582Run`, an SFTP upload over socket actions) finishes within the conformance runner's 20 s with BL-4008's socket-gated wait in place, and `Curl.Engine.UnitLibrary` stays inside the quality gates.

## Context

Split from BL-3989 (cost cap). BL-4008 lands the socket-gated wait; with it test582 hangs past 20 s, and it is on the ratchet and passes without it. Likely cause: an SSH read or write waiting in a direction the socket callback was not told, or `Waiting` being `None` while the transfer computes.

## Acceptance criteria

- [ ] The cause of the test582 hang is found and fixed, with a focused unit test in `Curl.Engine.UnitTests` that fails before the fix and passes after it (ADR-0482).
- [ ] Upstream test582 passes under `Curl.Conformance.UnitTests` and stays on the ratchet.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for the whole fast suite.
- [ ] `Measure-CodeQuality.ps1` reports `Curl.Engine.UnitLibrary` inside every gate.

## Notes

- Split from BL-3989 (cost cap). Builds on BL-4008's landed code, which holds the stashed work; read BL-4008 and BL-3989's Notes first.

## Log

- 2026-10-28: Created.
