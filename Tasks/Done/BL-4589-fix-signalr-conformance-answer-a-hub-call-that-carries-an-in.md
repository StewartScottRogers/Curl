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
completed: 2026-11-02
---
# BL-4589 — Fix SignalR conformance: answer a hub call that carries an invocation id with a Completion

## Goal

When a SignalR hub invokes a client method with an `invocationId` (a client result, HubProtocol.md "Invocation"), curl answers it with a Completion message carrying that id, so the hub does not wait for ever.

## Context

Found by BL-4491: `SignalRHubProtocolCases.HubCallWithAnInvocationId_IsAnsweredWithACompletion` (`SignalR:HUB-Invocation-3`) times out because `SignalRHubSession` writes the call as a line and sends nothing back. HubProtocol.md: the receiver of an Invocation with an `invocationId` sends a Completion. Curl implements no client methods, so the Completion carries an `error` naming the method (decide the text, ADR "Decided by Claude under Stewart's delegation").

## Acceptance criteria

- [x] The case carrying `SignalR:HUB-Invocation-3` in `Curl.Conformance.Extended.UnitTests/SignalR/` passes.
- [x] A focused unit test in `Curl.Extended.Protocol.SignalR.UnitTests` pins the fix (ADR-0482).
- [x] The `SignalR:HUB-Invocation-3` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` and the known-failures check passes.
- [x] `dotnet build` is clean, the fast tests are green and the library stays at 100% line and branch coverage (`Measure-CodeQuality.ps1`).

## Notes

- Plan: `SignalRHubSession.AcceptAsync` sends a type 3 Completion carrying the Invocation's `invocationId` and the error "Client didn't provide a result." (ASP.NET Core .NET client's text) after writing the call line; ADR-1202 records the choice. The Completion is written inline in `SignalRHubSession` (`RefusalOf`) so `HubMessageWriter`, outside touches, stays unchanged.
- Pinned by `SignalRProtocolHandlerTests.WithoutData_HubCallWithInvocationId_IsAnsweredWithACompletionError_HubProtocolInvocationAndCompletion`; the conformance case passes with the KnownFailures line removed (42 SignalR/known-failure conformance tests green).
- Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.SignalR.UnitLibrary: 0 failing members. Full fast run: 115 test projects passed. The change is platform-neutral (no paths, OS errors or certificates), which covers CI on Linux and macOS.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 8748c747 95af5e93 0753b55a 724857c1.
- 2026-11-02: Doing -> Done. A hub call carrying an invocationId is answered with a Completion error; SignalR:HUB-Invocation-3 passes
