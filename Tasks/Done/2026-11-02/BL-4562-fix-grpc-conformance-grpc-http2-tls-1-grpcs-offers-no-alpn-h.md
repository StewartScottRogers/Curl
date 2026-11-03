---
id: BL-4562
title: Fix Grpc conformance: Grpc:Http2-Tls-1 grpcs:// offers no ALPN h2
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcProtocolHandlerTests.cs, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4562 — Fix Grpc conformance: Grpc:Http2-Tls-1 grpcs:// offers no ALPN h2

## Goal

A `grpcs://` call offers ALPN `h2` in its ClientHello, so a gRPC server that selects `h2` takes the call.

## Context

Found by BL-4532's `GrpcTlsCases.Grpcs_ServerSelectsH2_OffersAlpnH2AndMakesTheCallOverTls` (requirement
`Grpc:Http2-Tls-1`, PROTOCOL-HTTP2.md: gRPC over TLS runs as HTTP/2 `h2`). Curl's ClientHello carries no ALPN extension
at all, and the call fails with exit 16 `The server at localhost did not negotiate h2 with ALPN, so it cannot take a gRPC
call`. Cause: `GrpcProtocolHandler.TargetOf` sets `ConnectTarget.ApplicationProtocols = ["h2"]` but no `PoolScheme`, and
`TcpConnector.ApplicationProtocolsFor` (`Curl.Networking.UnitLibrary/TcpConnector.cs`) offers a target's
`ApplicationProtocols` only when its `PoolScheme` is `https`, and nothing for any other scheme. The likely fix stays in the
extension: give the secure target `PoolScheme = "https"` (the handler never marks the connection reusable, so it is never
pooled). If the fix must go in `TcpConnector` instead, widen `touches` to it under the lane rules.

## Acceptance criteria

- [x] `GrpcTlsCases.Grpcs_ServerSelectsH2_OffersAlpnH2AndMakesTheCallOverTls` passes.
- [x] A focused unit test in `Curl.Extended.Protocol.Grpc.UnitTests` pins the fix (ADR-0482).
- [x] The `Grpc:Http2-Tls-1` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green; the library keeps its quality gates.

## Notes

- Fix stays in the extension (ADR-1197): `GrpcProtocolHandler.TargetOf` gives a `grpcs://` target `PoolScheme = "https"`, the one scheme `TcpConnector.ApplicationProtocolsFor` offers a target's `ApplicationProtocols` for; `grpc://` keeps none. `TcpConnector` left alone so no other TLS scheme's ALPN offer changes.
- Pinned by `ExecuteAsync_Grpcs_TargetsHttpsPoolSchemeSoTheHandshakeOffersAlpnH2` and `ExecuteAsync_Grpc_TargetsNoPoolScheme`; `GrpcTlsCases` (47 Grpc conformance cases) pass with `Grpc:Http2-Tls-1` off KnownFailures.
- Build -warnaserror clean, fast tests green, Measure-CodeQuality for the Grpc library: 0 failing members. Change is platform-neutral (no paths, OS text or certificates), so CI on Linux and macOS is covered.
- `ConnectTarget.PoolScheme`'s remark (Abstractions, outside touches) is now incomplete: filed BL-4576.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 5b215245 faa0a407 fc0f872b df930c3e.
- 2026-11-02: Doing -> Done. grpcs:// offers ALPN h2; Grpc:Http2-Tls-1 passes
