---
id: BL-6068
title: Retry a gRPC server reflection request once with the DPoP-Nonce its refusal gives
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-6055]
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/Reflection, Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcProtocolHandlerTests.cs, Curl.Extended.Protocol.Grpc.UnitTests/Reflection, Curl.Extension.Dpop.UnitLibrary/DpopTransferHook.cs, Curl.Extension.Dpop.UnitTests/DpopTransferHookTests.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcHttp2Call.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcClientException.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6068 — Retry a gRPC server reflection request once with the DPoP-Nonce its refusal gives

## Goal

With `--dpop`, a gRPC server reflection request refused with a 401 and a new `DPoP-Nonce` is retried once with a proof carrying that nonce.

## Context

Found in BL-6055 (ADR-1454). `ServerReflectionStream.ExchangeAsync` fails a refused reflection request with a `GrpcClientException` whose result carries no `TransferReport`, so `DpopTransferHook` never reads the `DPoP-Nonce` response header (RFC 9449 §9) and cannot retry. The refusal's HTTP status and response headers have to reach the hook in the result's report.

## Acceptance criteria

- [x] A test shows a reflection request answered 401 with `DPoP-Nonce` is retried once, the retry's reflection and call proofs carrying that nonce.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added to `touches`: `Client/GrpcHttp2Call.cs` (its `ResponseReader` builds the report, which held no response headers, so neither the reflection request nor the call could give the hook a nonce), `Client/GrpcClientException.cs` (carries the refusal's report) and the library's `CLAUDE.md`. No other task in Doing on `origin/work/dark-factory` named them.
- ADR-1456 records the decision: a gRPC response's report carries its Response-Headers, so a refused call is retried too and `-w %{header_json}` shows them. `DpopTransferHook` needed no change.
- Test: `ExecuteAsync_DpopReflectionRefusedWithANewNonce_RetriesOnceWithTheNonceInBothProofs_Rfc9449Section9`.
- Checks: `dotnet build` 0 warnings; `Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.Grpc.UnitLibrary` 0 failing members; gRPC tests 243 passed. The full fast run had one failure, `OciTransferCases.Verbose_Download_TellsTheConnectionAndTlsOnStandardErrorOnly` (`ObjectDisposedException` inside .NET's TLS credential cache, in a project this task does not change); its class passed 6 of 6 alone. Filed as BL-6070.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: c4ff3700 3d836d5e.
- 2026-11-26: Doing -> Done. With --dpop, a gRPC server reflection request refused with a new DPoP-Nonce is retried once with it
