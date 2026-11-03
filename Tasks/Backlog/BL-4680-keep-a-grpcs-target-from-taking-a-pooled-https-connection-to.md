---
id: BL-4680
title: Keep a grpcs target from taking a pooled https connection to the same origin
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcProtocolHandlerTests.cs, Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs, Curl.Networking.UnitLibrary/ConnectionPoolKey.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/ConnectionPoolKeyTests.cs]
requirement: none
created: 2026-11-03
completed:
---
# BL-4680 — Keep a grpcs target from taking a pooled https connection to the same origin

## Goal

A `grpcs://` transfer never reuses or shares a pooled `https://` connection to the same host and port, while its TLS handshake still offers ALPN `h2`.

## Context

Found by the shift-end review (ADR-0710) of `f37ba71c...dfa29449`: type-design-analyzer and code-reviewer.

`Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs` `TargetOf` (around line 156) now sets `PoolScheme = secure ? "https" : null` (ADR-1197) so that `TcpConnector.ApplicationProtocolsFor` offers ALPN. But `ConnectionPoolKey.Of` (`Curl.Networking.UnitLibrary/ConnectionPoolKey.cs:61`) makes every target with a `PoolScheme` poolable, and the key ignores `ApplicationProtocols`, so `https://h/` and `grpcs://h/` share one key. The doc on `ConnectTarget.PoolScheme` (`Curl.Protocol.Abstractions.UnitLibrary/ConnectTarget.cs` ~100-106) says such a target is never marked reusable; that covers handing a connection back, not taking one from the pool or sharing a multiplexed one.

Failure: `curl https://h/x grpcs://h/pkg.Svc/M` (or across `--next` groups with equal configuration) hands gRPC the HTTPS transfer's connection: an HTTP/1.1 one fails with exit 16 ("did not negotiate h2"), an h2 one gets a second connection preface written into a live session.

Fix options: a separate `ConnectTarget` flag that asks for the ALPN offer without making the target poolable, or a distinct pool scheme (`grpcs`) that `ApplicationProtocolsFor` and the `https` checks in `TcpConnector.cs` (~856, ~2209, ~2450) accept. Record the choice by amending ADR-1197.

## Acceptance criteria

- [ ] A unit test runs an `https://` transfer and then a `grpcs://` transfer to the same host and port over one `PoolingConnector` and shows the gRPC transfer opens its own connection (it is neither reused nor shared).
- [ ] The test `ExecuteAsync_Grpcs_TargetsHttpsPoolSchemeSoTheHandshakeOffersAlpnH2` (or its renamed successor) still shows the handshake offers `h2`.
- [ ] The `ConnectTarget.PoolScheme` doc comment and ADR-1197 say what the code now does.
- [ ] `dotnet build` is clean and the fast tests of the Grpc, Networking and Console unit test projects pass.

## Notes

## Log

- 2026-11-03: Created.
