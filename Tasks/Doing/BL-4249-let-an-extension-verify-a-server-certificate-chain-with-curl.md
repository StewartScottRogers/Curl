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
completed:
---
# BL-4249 — Let an extension verify a server certificate chain with curl's trust settings

## Goal

A protocol handler an extension adds can ask curl to verify a server certificate chain for a host name with the transfer's trust settings (`-k`, `--cacert`, `--capath`, `--pinnedpubkey`), getting the exit 60 or success curl's own TLS would give, without opening a TLS stream.

## Context

Found by BL-4116 (CoAP over DTLS): an extension that runs its own handshake over UDP (DTLS for `coaps://`) cannot verify the server's certificates the way curl does. `ProtocolHandlerServices` gives only `ITlsProvider`, which upgrades a stream; `Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs` is internal, and the trust options are not on `ITransferContext`. `Curl.Tls.UnitLibrary/IServerCertificateVerifier.cs` is the verifier shape the hand-built TLS and QUIC clients already take. Decide (ADR) the seam, e.g. a method on `ITlsProvider` returning a verifier per host, implemented in Networking over `HandBuiltCertificateVerifier`. A contract change in `Curl.Protocol.Abstractions`, so it runs apart from protocol tasks.

## Acceptance criteria

- [ ] `Curl.Protocol.Abstractions` exposes the seam, documented, and the engine's TLS provider implements it with the same trust settings its stream TLS uses.
- [ ] Unit tests show `-k` accepting any chain, a chain the given CA does not sign failing with exit 60, and a matching chain passing.
- [ ] An ADR marked "Decided by Claude under Stewart's delegation" records the seam.
- [ ] The changed libraries meet the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 320878b9 66862af5 16000991 478f867b 3d836d5e.
