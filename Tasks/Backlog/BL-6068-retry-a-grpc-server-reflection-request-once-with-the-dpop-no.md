---
id: BL-6068
title: Retry a gRPC server reflection request once with the DPoP-Nonce its refusal gives
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6055]
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/Reflection, Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcProtocolHandlerTests.cs, Curl.Extended.Protocol.Grpc.UnitTests/Reflection, Curl.Extension.Dpop.UnitLibrary/DpopTransferHook.cs, Curl.Extension.Dpop.UnitTests/DpopTransferHookTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6068 — Retry a gRPC server reflection request once with the DPoP-Nonce its refusal gives

## Goal

With `--dpop`, a gRPC server reflection request refused with a 401 and a new `DPoP-Nonce` is retried once with a proof carrying that nonce.

## Context

Found in BL-6055 (ADR-1454). `ServerReflectionStream.ExchangeAsync` fails a refused reflection request with a `GrpcClientException` whose result carries no `TransferReport`, so `DpopTransferHook` never reads the `DPoP-Nonce` response header (RFC 9449 §9) and cannot retry. The refusal's HTTP status and response headers have to reach the hook in the result's report.

## Acceptance criteria

- [ ] A test shows a reflection request answered 401 with `DPoP-Nonce` is retried once, the retry's reflection and call proofs carrying that nonce.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
