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
completed:
---
# BL-4340 — gRPC extension: create the project and the gRPC over HTTP/2 wire format


## Goal

`Curl.Extension.Grpc.UnitLibrary` exists in `/Extensions/Grpc/` in `Curl.slnx` with the gRPC over HTTP/2 wire format in its `Wire/` folder: request headers, length-prefixed messages and response trailers.

## Context

First of three parts of BL-4102 (split for size; then BL-4341 client flow, BL-4342 server reflection). Specification: grpc.io PROTOCOL-HTTP2.md. Extension rules: ADR-0999 and CLAUDE.md "Extensions"; use the `new-project` skill. The library may reference `Curl.Protocol.Abstractions`, `Curl.Extension.Abstractions`, `Curl.Http2.UnitLibrary` (HPACK, frames, `Http2Connection`) and `Curl.Protobuf.UnitLibrary`; never a `Curl.Protocol.*` library or another extension. No registration in `ExtensionCatalogue.cs` yet (BL-4341 does it).

## Acceptance criteria

- [ ] `Curl.Extension.Grpc.UnitLibrary` and `Curl.Extension.Grpc.UnitTests` exist in `/Extensions/Grpc/` in `Curl.slnx` and the build's `VerifyExtensionIsolation` passes.
- [ ] `Wire/` builds the request header list of PROTOCOL-HTTP2.md "Requests" (`:method POST`, `:scheme`, `:path /<service>/<method>`, `:authority`, `te: trailers`, `content-type: application/grpc`, `grpc-timeout` in its TimeoutValue/TimeoutUnit form, `user-agent`), with unit tests naming that section.
- [ ] `Wire/` frames and reads Length-Prefixed-Message (compressed flag, 4-byte big-endian length, message) per "Length-Prefixed-Message", including a message split across reads and a truncated one failing with a typed error; unit tests.
- [ ] `Wire/` reads the response "Responses" headers and trailers: `grpc-status` (the status code names), `grpc-message` percent-decoded, and Trailers-Only responses; unit tests.
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: eb6d7ef2 f9b63d98 3d27b122 25e23d36 e35aff77 3d836d5e.
