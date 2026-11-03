---
id: BL-4589
title: Fix SignalR conformance: answer a hub call that carries an invocation id with a Completion
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRHubSession.cs, Curl.Extended.Protocol.SignalR.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4589 — Fix SignalR conformance: answer a hub call that carries an invocation id with a Completion

## Goal

When a SignalR hub invokes a client method with an `invocationId` (a client result, HubProtocol.md "Invocation"), curl answers it with a Completion message carrying that id, so the hub does not wait for ever.

## Context

Found by BL-4491: `SignalRHubProtocolCases.HubCallWithAnInvocationId_IsAnsweredWithACompletion` (`SignalR:HUB-Invocation-3`) times out because `SignalRHubSession` writes the call as a line and sends nothing back. HubProtocol.md: the receiver of an Invocation with an `invocationId` sends a Completion. Curl implements no client methods, so the Completion carries an `error` naming the method (decide the text, ADR "Decided by Claude under Stewart's delegation").

## Acceptance criteria

- [ ] The case carrying `SignalR:HUB-Invocation-3` in `Curl.Conformance.Extended.UnitTests/SignalR/` passes.
- [ ] A focused unit test in `Curl.Extended.Protocol.SignalR.UnitTests` pins the fix (ADR-0482).
- [ ] The `SignalR:HUB-Invocation-3` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` and the known-failures check passes.
- [ ] `dotnet build` is clean, the fast tests are green and the library stays at 100% line and branch coverage (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 8748c747 95af5e93 0753b55a 724857c1.
