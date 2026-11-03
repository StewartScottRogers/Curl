---
id: BL-4599
title: Fix SocketIo conformance: SocketIo:EIO-MaxPayload-1 send no packet over the handshake's maxPayload
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SocketIo.UnitLibrary, Curl.Extended.Protocol.SocketIo.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4599 — Fix SocketIo conformance: SocketIo:EIO-MaxPayload-1 send no packet over the handshake's maxPayload

## Goal

Curl's socketio:// client sends no packet longer than the maxPayload of the Engine.IO open packet and fails the transfer instead.

## Context

Engine.IO v4: maxPayload in the open packet bounds what the server accepts. SocketIoSession sends the -d event whatever its size. Read maxPayload from the open packet; an event packet over it is not sent and the transfer fails (exit code by ADR, Decided by Claude under Stewart's delegation). Found by BL-4492's conformance run; the case is `SocketIoEngineIoCases.EventOverMaxPayload_IsNotSent` in `Curl.Conformance.Extended.UnitTests/SocketIo/`, matrix `Documentation/Conformance/Extended/SocketIo.md`.

## Acceptance criteria

- [x] `SocketIoEngineIoCases.EventOverMaxPayload_IsNotSent` passes as a plain case: the `SocketIo:EIO-MaxPayload-1 BL-4596` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` and the known-failures check passes.
- [x] A focused unit test in `Curl.Extended.Protocol.SocketIo.UnitTests` pins the fix (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green, and the library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Decided by Claude under Stewart's delegation (ADR-1211): the session reads `maxPayload` from the open packet (no limit when absent or not a whole non-negative number), measures every packet it sends (connect, event, ack, pong) as the UTF-8 bytes of the whole Engine.IO packet including its type, sends one of exactly `maxPayload` bytes, and ends a longer one with exit 55 (`CURLE_SEND_ERROR`) and `Socket.IO packet of <n> bytes is over the server's maxPayload of <m> bytes`. Upstream curl has no Socket.IO, so nothing to measure.
- Pinned by `SocketIoSessionTests.MaxPayloadOf_Handshake_IsMaxPayloadOrUnlimited`, `RunAsync_EventOverMaxPayload_IsNotSentAndFailsWithExit55` and `RunAsync_EventOfExactlyMaxPayload_IsSent`. `SocketIo:EIO-MaxPayload-1` removed from KnownFailures.txt; the SocketIo conformance cases and known-failures check pass (35/35).
- `dotnet build Curl.slnx -warnaserror` clean, fast tests green (115 projects); `Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.SocketIo.UnitLibrary`: 0 failing members. The change is platform-neutral (no paths, OS errors or certificates), which meets the CI-on-every-platform part.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: d4fdcd17 150bac4e ff8cc365.
- 2026-11-02: Doing -> Done. Socket.IO sends no packet over maxPayload; ends with exit 55 (ADR-1211)
