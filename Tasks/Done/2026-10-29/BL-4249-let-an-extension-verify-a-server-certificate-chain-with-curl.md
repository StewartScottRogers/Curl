---
id: BL-4249
title: Let an extension verify a server certificate chain with curl's trust settings
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4249 — Let an extension verify a server certificate chain with curl's trust settings

## Goal

A protocol handler an extension adds can ask curl to verify a server certificate chain for a host name with the transfer's trust settings (`-k`, `--cacert`, `--capath`, `--pinnedpubkey`), getting the exit 60 or success curl's own TLS would give, without opening a TLS stream.

## Context

Found by BL-4116 (CoAP over DTLS): an extension that runs its own handshake over UDP (DTLS for `coaps://`) cannot verify the server's certificates the way curl does. `ProtocolHandlerServices` gives only `ITlsProvider`, which upgrades a stream; `Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs` is internal, and the trust options are not on `ITransferContext`. `Curl.Tls.UnitLibrary/IServerCertificateVerifier.cs` is the verifier shape the hand-built TLS and QUIC clients already take. Decide (ADR) the seam, e.g. a method on `ITlsProvider` returning a verifier per host, implemented in Networking over `HandBuiltCertificateVerifier`. A contract change in `Curl.Protocol.Abstractions`, so it runs apart from protocol tasks.

## Acceptance criteria

- [x] `Curl.Protocol.Abstractions` exposes the seam, documented, and the engine's TLS provider implements it with the same trust settings its stream TLS uses.
- [x] Unit tests show `-k` accepting any chain, a chain the given CA does not sign failing with exit 60, and a matching chain passing.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records the seam.
- [x] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Seam (ADR-1052): ITlsProvider.VerifyServerCertificateChain(certificates, targetHost) returning ServerCertificateChainVerdict in Curl.Protocol.Abstractions; a default interface method that rejects with exit 60 (fail closed), overridden by SslStreamTlsProvider and HandBuiltTlsProvider through ServerCertificateVerification.VerifyChain over HandBuiltCertificateVerifier, the code the hand-built handshake uses. Method on the provider rather than a verifier factory: the provider already holds the trust settings and every handler already receives it.
- Tests: ServerCertificateVerificationTests.VerifyChain.cs pins -k, exit 60 (CA did not sign), acceptance (CA signed), exit 90 (pin) and exit 77 (missing --cacert) in both providers and both builds; platform-neutral (in-memory CA blob, SkipRevocationCheck, explicit build). Not measured against real curl: no new output text, the messages are the handshake's existing ones.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary,Curl.Networking.UnitLibrary: 0 failing members. Full build clean; fast tests green (0 failed).
- Filed BL-4262: CertificateCheckingTlsProvider (Curl.Extension.Abstractions) must forward the new method, outside this task's touches.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 320878b9 66862af5 16000991 478f867b 3d836d5e.
- 2026-10-29: Doing -> Done. Extensions verify a server certificate chain with curl's trust settings through ITlsProvider.VerifyServerCertificateChain
