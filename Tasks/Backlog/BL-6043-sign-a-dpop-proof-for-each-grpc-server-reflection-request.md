---
id: BL-6043
title: Sign a DPoP proof for each gRPC server reflection request
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/Reflection, Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcProtocolHandlerTests.cs, Curl.Extended.Protocol.Grpc.UnitTests/Reflection, Curl.Extension.Dpop.UnitLibrary/DpopTransferHook.cs, Curl.Extension.Dpop.UnitTests/DpopTransferHookTests.cs, Curl.Extension.Abstractions.UnitLibrary/Transfer]
requirement: none
created: 2026-11-26
completed:
---
# BL-6043 — Sign a DPoP proof for each gRPC server reflection request

## Goal

With `--dpop`, each gRPC server reflection request carries a DPoP proof naming its own HTTP `POST` and URL, with a unique `jti`.

## Context

Found in BL-6042 (ADR-1431). Without `--grpc-proto`, `GrpcProtocolHandler` learns the method's types by server
reflection on the same connection, sending the call's `-H` headers, and so the call's one DPoP proof, to
`/grpc.reflection.v1.ServerReflection/ServerReflectionInfo` (and `v1alpha`). RFC 9449 §4.2 wants `htu` to match each
request and `jti` to be unique. The handler needs a seam in `Curl.Extension.Abstractions` or its own context to ask for
a proof per request.

## Acceptance criteria

- [ ] A test shows each reflection request and the call carry proofs whose `htm` is `POST`, whose `htu` is that request's URL and whose `jti` is unique.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
