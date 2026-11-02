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
completed:
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

- [ ] `GrpcTlsCases.Grpcs_ServerSelectsH2_OffersAlpnH2AndMakesTheCallOverTls` passes.
- [ ] A focused unit test in `Curl.Extended.Protocol.Grpc.UnitTests` pins the fix (ADR-0482).
- [ ] The `Grpc:Http2-Tls-1` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests are green; the library keeps its quality gates.

## Notes

## Log

- 2026-11-02: Created.
