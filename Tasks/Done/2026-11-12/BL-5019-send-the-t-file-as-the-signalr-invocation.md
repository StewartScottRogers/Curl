---
id: BL-5019
title: Send the -T file as the signalr invocation
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs, Curl.Extended.Protocol.SignalR.UnitTests/Client/SignalRProtocolHandlerTests.cs, Documentation/Conformance/Extended/SignalR.md]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5019 — Send the -T file as the signalr invocation

## Goal

`curl -T file signalr://...` (and `signalrs://`) sends the file's contents as the hub invocation, exactly as `-d` would, instead of silently going into listen mode.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-031, signalr part (Medium, Confirmed). Data loss / silent wrong operation: yes. This task is only the signalr part; grpc, socketio and webtransport are BL-5018, BL-5020 and BL-5021.

**Repro.**
1. Run a mock on 127.0.0.1:47164 that answers `POST /hub/negotiate` with a WebSockets transport, completes the handshake, and answers type-1 invocations.
2. Create `inv.json` containing `{"type":1,"target":"A","arguments":[]}`.
3. `curl -sS -m 3 -T inv.json signalr://127.0.0.1:47164/hub`

**Expected.** The port's own convention, already applied to nats, stomp, mqtt5, redis, kafka, amqp and amqp1, is that `-T file` sends the file exactly as `-d` would. See `Documentation/Conformance/Extended/Nats.md` ("What `-T` does"), `Mqtt5.md` ("`-T` (upload)") and `Stomp.md` STOMP12-Send-3. A scheme that cannot use `-T` must instead fail with exit 2 before connecting; for signalr the convention (send the file) is the intended answer.

**Actual.** signalr never invokes anything. It goes into listen mode and ends with `curl: (28) Operation timed out after 3003 milliseconds with 0 bytes received`.

**Where to start.** `Curl.Extended.Protocol.SignalR.UnitLibrary/Client/SignalRProtocolHandler.cs` line 68 reads only `context.PostData`: read `context.Upload` when `PostData` is null, as `Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ProtocolHandler.cs` line 93 does.

## Acceptance criteria

- [x] A test in `Curl.Extended.Protocol.SignalR.UnitTests/Client/SignalRProtocolHandlerTests.cs` with an upload of `{"type":1,"target":"A","arguments":[]}` asserts the scripted hub receives an invocation with `"target":"A"` and the transfer does not time out.
- [x] Regression: `-d` with the same JSON sends the same invocation, and `-d` wins when both `-d` and `-T` are given; with neither, the handler still goes into listen mode as before.
- [x] `Documentation/Conformance/Extended/SignalR.md` gains a row stating `-T` sends the file as the invocation exactly as `-d` does, its "Covered by" naming the new test.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The unit tests use the existing `ScriptedConnector` in `Curl.Extended.Protocol.SignalR.UnitTests/Client`.
- BL-5043 also changes SignalR files (`Transport/SignalRNegotiator.cs`, `SignalR.md`); `SignalR.md` overlaps, so the two run one at a time.
- Review acceptance check: the signalr mock logs an invocation with `"target":"A"`.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: d474e6d6 076f57e2 11c2b0bb a0b49a29.
- 2026-11-12: Added CallBodyAsync (-d wins, else -T upload, else listen). Quality run: 0 failing members. SignalR tests 134 pass.
- 2026-11-12: Doing -> Done. -T upload sent as the signalr invocation
