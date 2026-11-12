---
id: BL-5018
title: Send the -T file as the grpc request message
priority: High
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcProtocolHandlerTests.cs, Documentation/Conformance/Extended/Grpc.md]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5018 — Send the -T file as the grpc request message

## Goal

`curl -T file grpc://...` (and `grpcs://`) sends the file's contents as the gRPC request message, exactly as `-d` would, instead of silently sending an empty message.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-031, grpc part (Medium, Confirmed). Data loss / silent wrong operation: yes. This task is only the grpc part; signalr, socketio and webtransport are BL-5019, BL-5020 and BL-5021.

**Repro.**
1. Run an HTTP/2 mock on 127.0.0.1:47163. It answers SETTINGS, replies with HEADERS `:status 200` and `content-type: application/grpc`, then DATA `00 00000004 0a026869`, then trailers `grpc-status: 0`, and logs every frame.
2. Create `t.proto` containing `syntax="proto3"; package t; message Req{string name=1;} message Rep{string msg=1;} service S{rpc M(Req) returns (Rep);}` and `req.json` containing `{"name":"zed"}`.
3. `curl -sS -m 4 --grpc-proto t.proto -T req.json grpc://127.0.0.1:47163/t.S/M`

**Expected.** The port's own convention, already applied to nats, stomp, mqtt5, redis, kafka, amqp and amqp1, is that `-T file` sends the file exactly as `-d` would. See `Documentation/Conformance/Extended/Nats.md` ("What `-T` does"), `Mqtt5.md` ("`-T` (upload)") and `Stomp.md` STOMP12-Send-3. A scheme that cannot use `-T` must instead fail with exit 2 before connecting; for grpc the convention (send the file) is the intended answer.

**Actual.** grpc sends an empty message (DATA `00 00000000`), prints the reply, and exits 0.

**Where to start.** `Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs` lines 116 and 128, both `GrpcMessageJson.Encode(method.Input, context.PostData)`: read `context.Upload` when `PostData` is null, as `Curl.Extended.Protocol.Mqtt5.UnitLibrary/Client/Mqtt5ProtocolHandler.cs` line 93 does (`context.PostData is not null || context.Upload is not { } upload`). This is the `FromProtoFile` path (with `--grpc-proto`) and the `CallOverAsync` path (reflection).

## Acceptance criteria

- [x] A test in `Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcProtocolHandlerTests.cs` with `--grpc-proto` and an upload of `{"name":"zed"}` asserts the DATA frame the scripted server sees is `00 00000005 0a037a6564`.
- [x] A second test with no `--grpc-proto` (server reflection path) and the same upload asserts the same DATA frame.
- [x] Regression: `-d '{"name":"zed"}'` still sends `00 00000005 0a037a6564`, and `-d` wins when both `-d` and `-T` are given (as in the Mqtt5 handler).
- [x] `Documentation/Conformance/Extended/Grpc.md` gains a row stating `-T` sends the file as the request message exactly as `-d` does, its "Covered by" naming the new test.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The unit tests use the existing `ScriptedConnection` / `GrpcServerReply` in `Curl.Extended.Protocol.Grpc.UnitTests/Client`.
- BL-5028 and BL-5045 also change `GrpcProtocolHandler.cs`, so the board runs them one at a time; no dependency is needed.
- Review acceptance check: the grpc mock logs DATA `00 00000005 0a037a6564`.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 64762c04 3babe6fb de49f53e 8a0eba5a a0b49a29.
- 2026-11-12: -T read in GrpcProtocolHandler.RequestBodyAsync (-d wins); 3 tests; Grpc.md row Request-16; quality run 100/100, 0 failing. CI criterion met by platform-neutral tests.
- 2026-11-12: Doing -> Done. -T file sent as grpc request message
