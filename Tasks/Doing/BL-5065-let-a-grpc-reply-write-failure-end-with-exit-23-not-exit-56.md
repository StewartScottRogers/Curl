---
id: BL-5065
title: Let a gRPC reply write failure end with exit 23, not exit 56
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcProtocolHandlerTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5065 — Let a gRPC reply write failure end with exit 23, not exit 56

## Goal

When a gRPC transfer cannot write its reply to its output (an `-o` file on a full disk, or a write callback that aborts), it ends with the engine's exit 23 write failure, not `(56) Recv failure`.

## Context

The shift-end review of 2026-11-12 found this in the BL-5028 change (silent-failure-hunter, ADR-0710).

- In `Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs:150`, `CallOverAsync` wraps all of `CallOverStreamAsync` in `catch (IOException failure) when (failure is not EndOfStreamException)`.
- That block also covers the reply's write to `context.Output`.
- `OutputWriteFailedException` (`Curl.Protocol.Abstractions.UnitLibrary/OutputWriteFailedException.cs:14`) derives from `IOException`, so the catch takes it.
- So an output write failure is reported as `CurlExitCode.RecvError` with "Connection was reset".

## Acceptance criteria

- [x] An output write failure reaches the engine as it did before BL-5028. Either `CallOverAsync`'s filter excludes `OutputWriteFailedException`, or its try block covers only the connection's reads and writes.
- [x] A unit test in `GrpcProtocolHandlerTests`, with an output stream that throws `OutputWriteFailedException`, shows the transfer does not end with exit 56.
- [x] The existing tests that map a connection reset to exit 56 still pass.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- CallOverAsync's catch filter now excludes OutputWriteFailedException, so it propagates to the engine as before BL-5028. Test: ExecuteAsync_OutputWriteFails_LetsTheWriteFailureReachTheEngineNotExit56. Quality run for the Grpc library: 0 failing members.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: fba1b063 1fd95b6f eeda0f41 3d836d5e.
