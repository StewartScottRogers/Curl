---
id: BL-4340
title: gRPC extension: create the project and the gRPC over HTTP/2 wire format
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4099]
touches: [Curl.Extension.Grpc.UnitLibrary/Curl.Extension.Grpc.UnitLibrary.csproj, Curl.Extension.Grpc.UnitLibrary/CLAUDE.md, Curl.Extension.Grpc.UnitLibrary/Wire, Curl.Extension.Grpc.UnitTests/Curl.Extension.Grpc.UnitTests.csproj, Curl.Extension.Grpc.UnitTests/Wire, Curl.slnx]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4340 — gRPC extension: create the project and the gRPC over HTTP/2 wire format


## Goal

`Curl.Extension.Grpc.UnitLibrary` exists in `/Extensions/Grpc/` in `Curl.slnx` with the gRPC over HTTP/2 wire format in its `Wire/` folder: request headers, length-prefixed messages and response trailers.

## Context

First of three parts of BL-4102 (split for size; then BL-4341 client flow, BL-4342 server reflection). Specification: grpc.io PROTOCOL-HTTP2.md. Extension rules: ADR-0999 and CLAUDE.md "Extensions"; use the `new-project` skill. The library may reference `Curl.Protocol.Abstractions`, `Curl.Extension.Abstractions`, `Curl.Http2.UnitLibrary` (HPACK, frames, `Http2Connection`) and `Curl.Protobuf.UnitLibrary`; never a `Curl.Protocol.*` library or another extension. No registration in `ExtensionCatalogue.cs` yet (BL-4341 does it).

## Acceptance criteria

- [x] `Curl.Extension.Grpc.UnitLibrary` and `Curl.Extension.Grpc.UnitTests` exist in `/Extensions/Grpc/` in `Curl.slnx` and the build's `VerifyExtensionIsolation` passes.
- [x] `Wire/` builds the request header list of PROTOCOL-HTTP2.md "Requests" (`:method POST`, `:scheme`, `:path /<service>/<method>`, `:authority`, `te: trailers`, `content-type: application/grpc`, `grpc-timeout` in its TimeoutValue/TimeoutUnit form, `user-agent`), with unit tests naming that section.
- [x] `Wire/` frames and reads Length-Prefixed-Message (compressed flag, 4-byte big-endian length, message) per "Length-Prefixed-Message", including a message split across reads and a truncated one failing with a typed error; unit tests.
- [x] `Wire/` reads the response "Responses" headers and trailers: `grpc-status` (the status code names), `grpc-message` percent-decoded, and Trailers-Only responses; unit tests.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Delivered directly rather than through the full `/feature` subagent stages: a self-contained wire layer in a new library, and the run's cost cap would not hold five subagents. Decisions in ADR-1133 (1129 was taken on the shared branch).
- `Wire/`: `GrpcRequestHeaders` + `GrpcRequestTarget` + `GrpcTimeout` (finest unit within 8 digits, rounded up); `LengthPrefixedMessage` (frame) and `LengthPrefixedMessageReader` (split reads, several per read, truncation/bad flag/over-long as `GrpcWireException`, 4 MiB default limit); `GrpcResponseHeaders` (Trailers-Only, HTTP status mapping per http-grpc-status-mapping.md, content-type check, trailers), `GrpcStatus`/`GrpcStatusCode` (statuscodes.md names), `GrpcStatusMessage` (lenient percent-decoding).
- The library references only Protocol.Abstractions and Extension.Abstractions so far; BL-4341 adds Http2/Protobuf when it needs them. Header lists are `KeyValuePair<string, string>`.
- Measure-CodeQuality.ps1 -Library Curl.Extension.Grpc.UnitLibrary: 100% line, 100% branch, max complexity 8, 0 failing members. 80 tests in Curl.Extension.Grpc.UnitTests; full build clean, fast tests green. All tests are pure in-memory, platform-neutral, so CI on Linux and macOS is covered.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: eb6d7ef2 f9b63d98 3d27b122 25e23d36 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. Curl.Extension.Grpc.UnitLibrary exists with the gRPC over HTTP/2 wire format: request headers, length-prefixed messages, response headers and trailers
