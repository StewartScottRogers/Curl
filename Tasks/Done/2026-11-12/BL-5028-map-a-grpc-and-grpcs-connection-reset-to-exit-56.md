---
id: BL-5028
title: Map a grpc and grpcs connection reset to exit 56
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcProtocolHandlerTests.cs, Documentation/Conformance/Extended/Grpc-Transport.md]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5028 — Map a grpc and grpcs connection reset to exit 56

## Goal

A connection reset during a grpc or grpcs call exits 56 with a `curl: (56) Recv failure: Connection was reset` line (55 when the reset happens while sending), like every other extension scheme, instead of exit 2 with raw .NET text.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-032, grpc/grpcs part (Medium, Confirmed). The mcps part is BL-5029.

**Repro.**
- grpc: run a TCP server on 127.0.0.1:47171 that accepts, reads for 0.5 s, then closes with `SO_LINGER {1,0}`, which sends an RST. Use `t.proto` with `syntax="proto3"; package t; message Req{string name=1;} message Rep{string msg=1;} service S{rpc M(Req) returns (Rep);}`. Run `curl -sS -m 4 --grpc-proto t.proto -d '{"name":"x"}' grpc://127.0.0.1:47171/t.S/M`. The result is the same without `--grpc-proto`.
- grpcs: run a TLS server on 127.0.0.1:46040 with a self-signed certificate that completes the handshake, reads once with a 2 s timeout, and closes. Run `curl -sS -k -m 6 grpcs://127.0.0.1:46040/a.B/C`.

**Expected.** Every other extension scheme against the same servers exits 56: nats, socketio and signalr print `curl: (56) Recv failure: Connection was reset`; stomp, redis, amqp, amqp1, mqtt5, mqtt5+ws and kafka exit 56; xmpps, stomps, natss, kafkas, rediss, amqp1s, amqps, mqtt5s, socketios and signalrs exit 56.

**Actual.** `curl: (2) Unable to read data from the transport connection: An existing connection was forcibly closed by the remote host..` (raw .NET text, doubled period), exit 2, on both grpc commands.

**Where to start.** `Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs` lines 40-56 catch the gRPC and HTTP/2 exceptions and `EndOfStreamException`, but not `IOException` or `SocketException`. Map them to 56, or to 55 when sending. Reuse the mapping a sibling extension already has (for example the nats or signalr handler) so the wording is identical.

## Acceptance criteria

- [x] Tests in `Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcProtocolHandlerTests.cs` script a connection that throws `IOException` wrapping a `SocketException` (connection reset) while reading the reply, and assert exit 56 with the message `Recv failure: Connection was reset`, for both the `--grpc-proto` path and the server-reflection path.
- [x] A test scripts the reset while the request is being written and asserts exit 55.
- [x] The messages contain no raw .NET text and no doubled period.
- [x] Regression: the existing `EndOfStreamException`, GOAWAY and RST_STREAM mappings keep their current exit codes and messages.
- [x] `Documentation/Conformance/Extended/Grpc-Transport.md` gains a row stating the reset-to-56 (55 when sending) mapping, its "Covered by" naming the new tests.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The unit tests use the existing `ScriptedConnection` in `Curl.Extended.Protocol.Grpc.UnitTests/Client`.
- BL-5018 and BL-5045 also change `GrpcProtocolHandler.cs`, so the board runs them one at a time; no dependency is needed.
- Review acceptance check: both grpc and grpcs commands print `curl: (56) Recv failure: Connection was reset` (or a similar `(56)` line) and exit 56.

- Decided: reused the nats/signalr constant wording (`Recv failure: Connection was reset`), not per-platform words, so tests are platform-neutral; a write-side IOException (tracked in ConnectionStream) gives 55 `Send failure: Connection was reset`. EndOfStreamException keeps exit 16. grpcs shares the same code path. Quality run: Grpc library 100/100, no failing members. CI-on-Linux criterion met by platform-neutral tests.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 66cef6f7 87757494 b0fc451b 8df7dfda 6f3a341d a0b49a29.
- 2026-11-12: Doing -> Done. grpc and grpcs connection reset now exits 56 (55 when sending) with curl's wording
