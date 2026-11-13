---
id: BL-5140
title: Write each gRPC server-streaming reply as it arrives so a -m timeout keeps the messages already received (EXT-047)
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcHttp2Call.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcCallResult.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcHttp2Session.cs, Curl.Extended.Protocol.Grpc.UnitTests/Client, Documentation/Conformance/Extended/Grpc-Responses.md]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5140 — Write each gRPC server-streaming reply as it arrives so a -m timeout keeps the messages already received (EXT-047)

## Goal

A gRPC (`grpc://`, `grpcs://`) server-streaming call writes each reply message to the output as it is decoded and counts it in `%{size_download}`, so a `-m` timeout keeps every message already received.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-047 (Medium, Confirmed, 4 runs with and without `--grpc-proto`). Data loss / silent wrong output.

**Repro.**
1. Run a gRPC server on 127.0.0.1:27010 (the review used grpcio; a PowerShell or C# file-based stand-in is fine, never Python) serving `t.proto`: `syntax="proto3"; package t; message Req{string name=1; int32 n=2;} message Rep{string msg=1;} service S{ rpc Stream(Req) returns (stream Rep); }`. `Stream` yields `Rep(msg=name+str(i))` for `i` in `range(n)`, sleeping 1 s after each when `name` is `"slow"`. Enable server reflection v1.
2. `curl -sS -m 2.5 -w 'size=%{size_download}\n' -d '{"name":"slow","n":5}' grpc://127.0.0.1:27010/t.S/Stream`
3. Timing check: `curl -sS -m 8 -d '{"name":"slow","n":4}' grpc://127.0.0.1:27010/t.S/Stream` with each stdout read timestamped.

**Expected.** `Documentation/Conformance/Extended/Grpc-Responses.md` row `Grpc:Http2-Response-2`: a server-streaming reply "prints every message, in order"; ADR-1147: messages that came before the status are still written; the port's other streaming schemes (nats, signalr, mqtt5, stomp) write each message as it arrives and keep it on a timeout. Step 2 should print `slow0`, `slow1`, `slow2` (as the JSON objects), then `curl: (28) ...`, with `size=` greater than 0.

**Actual.** Step 2 prints nothing on stdout, then `curl: (28) Operation timed out after 2510 milliseconds with 0 bytes received`, `size=0`, exit 28. In step 3 all messages reach stdout together at 4.07 s, when the call ends.

**Where to start.** `Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcHttp2Call.cs` collects messages in `Messages` (`List<GrpcMessage>`, lines 78 and 128) and returns them in `GrpcCallResult` only after the trailers (line 61); `GrpcProtocolHandler.WriteAsync` (lines 178-189) writes them afterwards. Write each message to the output as it is decoded and report the downloaded bytes (as `SignalRHubSession.cs:136-137` does with `progress.ReportDownloaded`). `GrpcCallResult(Messages, Status, Report)` must keep carrying what `-w` and the status handling still need; unary and `--grpc-proto` JSON rendering must produce the same bytes as before.

## Acceptance criteria

- [x] New tests in `Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcStreamingOutputTests.cs` (new, using the existing `ScriptedConnection`/`GrpcServerReply`) prove that the first message of a server-streaming reply is written to the output before the later messages and the trailers arrive (the scripted connection is held open after the first message and the written bytes are asserted at that point).
- [x] A test proves that when the `-m` timeout fires after three of five messages, the three messages are in the output, the exit is `CurlExitCode.OperationTimedOut` (28) and the reported downloaded byte count equals the bytes written.
- [x] Regression tests prove a unary call and a completed server-streaming call write the same bytes as before, and that a call that ends with a non-OK status still writes the messages that preceded it (ADR-1147).
- [x] `Documentation/Conformance/Extended/Grpc-Responses.md` row `Grpc:Http2-Response-2` (or a new row) states that each message is written as it arrives and kept on a timeout, "Covered by" naming a test from this task.
- [x] The tests are platform-neutral (no real socket).
- [x] `dotnet build Curl.Extended.Protocol.Grpc.UnitLibrary -warnaserror` is clean, `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Review acceptance check, for a hand check: step 2 prints the JSON objects for `slow0`, `slow1` and `slow2` before `curl: (28)`, and `size=` equals the stdout byte count.
- Keep `--ai-help` and `--extended-help` right if any text there says replies are printed together.

- Done: GrpcHttp2Call.RunAsync takes an onMessage callback; the handler writes, flushes and reports each message as it is decoded (GrpcCallResult no longer carries Messages). Tests in GrpcStreamingOutputTests. Grpc-Responses.md prose names them (the matrix Covered-by column must name one SpecRequirement carrier, so the row is unchanged). Quality run: 0 failing members. Ftp.UnitTests had one unrelated failure in the full run that passed alone.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 7feb67e1 797f2631 0bd81425 a025b6ed d6cd4299 77cac303.
- 2026-11-13: Doing -> Done. gRPC server-streaming messages are written and counted as they arrive, kept on -m timeout
