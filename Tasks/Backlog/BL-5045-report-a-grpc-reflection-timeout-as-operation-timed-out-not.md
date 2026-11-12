---
id: BL-5045
title: Report a grpc reflection timeout as Operation timed out, not Connection timed out
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcProtocolHandlerTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5045 — Report a grpc reflection timeout as Operation timed out, not Connection timed out

## Goal

A `-m` timeout during gRPC server reflection on an established connection reports `Operation timed out after N milliseconds with 0 bytes received`, not `Connection timed out after N milliseconds`.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip), finding EXT-037 (Low, Confirmed). The exit code (28) is already correct.

**Repro.**
1. Run a TCP server on 127.0.0.1:47181 that accepts connections and never replies.
2. `curl -sS -m 2 -d '{"name":"bob"}' grpc://127.0.0.1:47181/t.S/M` (no `--grpc-proto`, so server reflection runs first).

**Expected.** `curl: (28) Operation timed out after 2000 milliseconds with 0 bytes received`, as the port prints with `--grpc-proto` against the same server, and for http, nats, stomp and signalr.

**Actual.** `curl: (28) Connection timed out after 2008 milliseconds`.

**Where to start (verified against the source).** The wording is chosen by `Curl.Core.UnitLibrary/MaxTimeWatchdog.cs` (`Failure`): `Operation timed out ...` once the handler has called `context.Progress.ReportTransferStarted()`, `Connection timed out ...` before. In `Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs` `CallOverAsync` only calls `ReportTransferStarted()` (line 129) after `ServerReflectionClient.DescribeAsync` returns, so a timeout during reflection looks like a connect timeout. Call `ReportTransferStarted()` once the HTTP/2 session is open, before reflection (the review named `Reflection/ServerReflectionClient.cs`, but `DescribeAsync` has no `context`, so the call belongs in the handler). Nats does the same at `NatsProtocolHandler.cs:69`.

## Acceptance criteria

- [ ] A test in `Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcProtocolHandlerTests.cs` scripts a connection that accepts, completes the HTTP/2 preface and never answers the reflection call, with a `-m` of 2 s on a fake `TimeProvider`, and asserts exit 28 with the message `Operation timed out after 2000 milliseconds with 0 bytes received`.
- [ ] Regression: a timeout while still connecting (no connection established) still says `Connection timed out after N milliseconds`, and the `--grpc-proto` timeout text is unchanged.
- [ ] `ReportTransferStarted` is called exactly once per call (a test with a recording `ITransferProgress` asserts it for both the reflection and the `--grpc-proto` paths).
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). The unit tests use the existing `ScriptedConnection` in `Curl.Extended.Protocol.Grpc.UnitTests/Client`.
- BL-5018 and BL-5028 also change `GrpcProtocolHandler.cs`, so the board runs them one at a time; no dependency is needed.
- Review acceptance check: the command prints the `Operation timed out after ... with 0 bytes received` text.

## Log

- 2026-11-12: Created.
