---
id: BL-6055
title: Sign a DPoP proof for each gRPC server reflection request
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Grpc.UnitLibrary/Client/GrpcProtocolHandler.cs, Curl.Extended.Protocol.Grpc.UnitLibrary/Reflection, Curl.Extended.Protocol.Grpc.UnitTests/Client/GrpcProtocolHandlerTests.cs, Curl.Extended.Protocol.Grpc.UnitTests/Reflection, Curl.Extension.Dpop.UnitLibrary/DpopTransferHook.cs, Curl.Extension.Dpop.UnitTests/DpopTransferHookTests.cs, Curl.Extension.Abstractions.UnitLibrary/Transfer, Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md, Curl.Extension.Abstractions.UnitTests/Transfer/TransferContextRequestSigningTests.cs, Curl.Extension.OsCredentialStore.UnitLibrary/CredentialedTransferContext.cs, Curl.Extension.OsCredentialStore.UnitTests/CredentialedTransferContextTests.cs, Curl.Extension.Dpop.UnitLibrary/CLAUDE.md, Curl.Extended.Protocol.Grpc.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6055 — Sign a DPoP proof for each gRPC server reflection request

## Goal

With `--dpop`, each gRPC server reflection request carries a DPoP proof naming its own HTTP `POST` and URL, with a unique `jti`.

## Context

Found in BL-6042 (ADR-1439). Without `--grpc-proto`, `GrpcProtocolHandler` learns the method's types by server
reflection on the same connection, sending the call's `-H` headers, and so the call's one DPoP proof, to
`/grpc.reflection.v1.ServerReflection/ServerReflectionInfo` (and `v1alpha`). RFC 9449 §4.2 wants `htu` to match each
request and `jti` to be unique. The handler needs a seam in `Curl.Extension.Abstractions` or its own context to ask for
a proof per request.

## Acceptance criteria

- [x] A test shows each reflection request and the call carry proofs whose `htm` is `POST`, whose `htu` is that request's URL and whose `jti` is unique.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Design (ADR-1454): `RequestHeadersSigner` in `Curl.Extension.Abstractions/Transfer`; `--dpop` puts one on the
  transfer and the gRPC handler asks `SignedHeadersFor` for the reflection stream. It is found through
  `IWrappingTransferContext`, since hooks listed after `--dpop`'s wrap the context again.
- `touches` widened (no other task in Doing named any of them on origin/work/dark-factory):
  `CredentialedTransferContext` and its test (`--user-from-keychain` wraps the context after `--dpop`, so it must
  implement `IWrappingTransferContext`), the new `TransferContextRequestSigningTests.cs`, and the `CLAUDE.md` of the
  Abstractions, DPoP and gRPC libraries.
- Only `v1` reflection exists in the code, and the signer is applied to whatever service name the stream posts to. One
  reflection stream is one HTTP request, so one proof.
- Verified: `dotnet build` clean (0 warnings), fast tests all pass, `Measure-CodeQuality.ps1` on the four changed
  libraries 100% line and branch, 0 failing members. Platform-neutral (temporary directory only).
- Follow-up filed: BL-6068 (retry a refused reflection request with its `DPoP-Nonce`).

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: f38156b0 3d836d5e.
- 2026-11-26: Doing -> Done. Each gRPC server reflection request carries a DPoP proof naming its own POST and URL, with its own jti (ADR-1454)
