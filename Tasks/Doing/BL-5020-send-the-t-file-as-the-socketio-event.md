---
id: BL-5020
title: Send the -T file as the socketio event
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoProtocolHandler.cs, Curl.Extended.Protocol.SocketIo.UnitTests/SocketIoProtocolHandlerTests.cs, Documentation/Conformance/Extended/SocketIo.md]
requirement: none
created: 2026-11-12
completed:
---
# BL-5020 — Send the -T file as the socketio event

## Goal

`curl -T file socketio://...` (and `socketios://`) sends the file's contents as the Socket.IO event, exactly as `-d` would, and the file name is never appended to the namespace.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-031, socketio part (Medium, Confirmed). Data loss / silent wrong operation: yes. This task is only the socketio part; grpc, signalr and webtransport are BL-5018, BL-5019 and BL-5021.

**Repro.**
1. Run an Engine.IO v4 mock on 127.0.0.1:47165 that logs every frame.
2. Create `ev.json` containing `["e",1]`.
3. `curl -sS -m 4 -T ev.json socketio://127.0.0.1:47165/ns`

**Expected.** The port's own convention, already applied to nats, stomp, mqtt5, redis, kafka, amqp and amqp1, is that `-T file` sends the file exactly as `-d` would. See `Documentation/Conformance/Extended/Nats.md` ("What `-T` does"), `Mqtt5.md` ("`-T` (upload)") and `Stomp.md` STOMP12-Send-3. A scheme that cannot use `-T` must instead fail with exit 2 before connecting; for socketio the convention (send the file) is the intended answer.

**Actual.** socketio joins `/ns`, sends no `42` event, prints the received events, and exits 0. With a URL ending in `/`, the file name is appended and the namespace becomes `/ev.json`.

**Where to start.** `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoProtocolHandler.cs` line 54 reads only `context.PostData`: read `context.Upload` when `PostData` is null, as `Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ProtocolHandler.cs` line 93 does. The appended-file-name namespace comes from the URL the core retargets for an upload; the handler must use the namespace of the URL as typed (check the retargeted/original URL on the context). If the fix is not in the handler, add the file to `touches`.

## Acceptance criteria

- [ ] A test in `Curl.Extended.Protocol.SocketIo.UnitTests/SocketIoProtocolHandlerTests.cs` with an upload of `["e",1]` to `/ns` asserts the scripted server receives `42/ns,["e",1]`.
- [ ] A test with a URL ending in `/` (`socketio://host/`) and `-T ev.json` asserts the namespace stays `/` (not `/ev.json`) and the event is sent.
- [ ] Regression: `-d '["e",1]'` still sends the same event, `-d` wins when both are given, and with neither the handler still only listens.
- [ ] `Documentation/Conformance/Extended/SocketIo.md` gains a row stating `-T` sends the file as the event exactly as `-d` does and does not change the namespace, its "Covered by" naming the new tests.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The unit tests use the existing `ScriptedConnection` / `ScriptedConnector` in `Curl.Extended.Protocol.SocketIo.UnitTests`.
- Review acceptance check: the socketio mock logs `42/ns,["e",1]`.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 1b19d2d0 f0275f32 e28c4069 a40a5c8a a0b49a29.
