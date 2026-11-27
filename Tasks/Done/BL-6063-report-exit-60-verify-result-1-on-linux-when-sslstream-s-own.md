---
id: BL-6063
title: Report exit 60 verify result 1 on Linux when SslStream's own chain build throws for OpenSSL error 1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitLibrary/CertificateChainBuild.cs, Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitLibrary/CLAUDE.md, Curl.Networking.UnitTests/SslStreamTlsProviderTests.UnspecifiedChain.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6063 — Report exit 60 verify result 1 on Linux when SslStream's own chain build throws for OpenSSL error 1

## Goal

On Linux a server certificate that makes OpenSSL answer error 1 during the `SslStream` handshake is exit 60 `SSL certificate OpenSSL verify result: unspecified certificate verification error (1)`, as OpenSSL curl reports it.

## Context

Found in BL-6061 (ADR-1446). .NET on Linux builds the chain inside the handshake and throws `CryptographicException` ("... unhandled error code '1'.") before the validation callback, so the failure is caught in `SslStreamTlsProvider`'s handshake and reported as a connect error (exit 35), under `-k` too, where curl goes on. `CertificateChainBuild` already handles the builds Curl makes itself. Read ADR-1442 and ADR-1446; decide what `-k` can do here (routing to the hand-built client is one option).

## Acceptance criteria

- [x] A unit test that makes the handshake step throw that `CryptographicException` in the OpenSSL build gets exit 60 and the message above, on every platform; `-k`'s behaviour is decided, recorded in an ADR and pinned by a test.

## Notes

- Decision (ADR-1448): the OpenSSL build answers exit 60 with verify result 1; under `-k` the exit 35 connect error stands and only the verify result is added, since the handshake is already over. Completing it needs a second dial on the hand-built client: BL-6064.
- The throw is matched bare or as an inner exception, because which form `SslStream` on Linux raises was not observed here (lanes run on Windows).
- `touches` widened to `CertificateChainBuild.cs` (so .NET's message is matched in one place), `TlsFailureMessages.cs` (where every exit 60 message lives) and the library's `CLAUDE.md`; no other task in Doing named them.
- Tests are platform-neutral (the handshake step is replaced). `dotnet build` clean, fast tests green, `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 0 failing members.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 809eaa5e.
- 2026-11-26: Doing -> Done. The OpenSSL build reports SslStream's own chain build throwing for OpenSSL error 1 as exit 60 with verify result 1 (ADR-1448); -k is filed as BL-6064.
