---
id: BL-4596
title: Fix SocketIo conformance: SocketIo:EIO-MaxPayload-1 send no packet over the handshake's maxPayload
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.SocketIo.UnitLibrary, Curl.Extended.Protocol.SocketIo.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4596 — Fix SocketIo conformance: SocketIo:EIO-MaxPayload-1 send no packet over the handshake's maxPayload

## Goal

Curl's socketio:// client sends no packet longer than the maxPayload of the Engine.IO open packet and fails the transfer instead.

## Context

Engine.IO v4: maxPayload in the open packet bounds what the server accepts. SocketIoSession sends the -d event whatever its size. Read maxPayload from the open packet; an event packet over it is not sent and the transfer fails (exit code by ADR, Decided by Claude under Stewart's delegation). Found by BL-4492's conformance run; the case is `SocketIoEngineIoCases.EventOverMaxPayload_IsNotSent` in `Curl.Conformance.Extended.UnitTests/SocketIo/`, matrix `Documentation/Conformance/Extended/SocketIo.md`.

## Acceptance criteria

- [ ] `SocketIoEngineIoCases.EventOverMaxPayload_IsNotSent` passes as a plain case: the `SocketIo:EIO-MaxPayload-1 BL-4596` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` and the known-failures check passes.
- [ ] A focused unit test in `Curl.Extended.Protocol.SocketIo.UnitTests` pins the fix (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green, and the library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

## Log

- 2026-11-02: Created.
