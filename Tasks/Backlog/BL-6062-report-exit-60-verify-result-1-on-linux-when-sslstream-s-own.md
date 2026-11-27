---
id: BL-6062
title: Report exit 60 verify result 1 on Linux when SslStream's own chain build throws for OpenSSL error 1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitTests/SslStreamTlsProviderTests.UnspecifiedChain.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6062 — Report exit 60 verify result 1 on Linux when SslStream's own chain build throws for OpenSSL error 1

## Goal

On Linux a server certificate that makes OpenSSL answer error 1 during the `SslStream` handshake is exit 60 `SSL certificate OpenSSL verify result: unspecified certificate verification error (1)`, as OpenSSL curl reports it.

## Context

Found in BL-6061 (ADR-1445). .NET on Linux builds the chain inside the handshake and throws `CryptographicException` ("... unhandled error code '1'.") before the validation callback, so the failure is caught in `SslStreamTlsProvider`'s handshake and reported as a connect error (exit 35), under `-k` too, where curl goes on. `CertificateChainBuild` already handles the builds Curl makes itself. Read ADR-1442 and ADR-1445; decide what `-k` can do here (routing to the hand-built client is one option).

## Acceptance criteria

- [ ] A unit test that makes the handshake step throw that `CryptographicException` in the OpenSSL build gets exit 60 and the message above, on every platform; `-k`'s behaviour is decided, recorded in an ADR and pinned by a test.

## Notes

## Log

- 2026-11-26: Created.
