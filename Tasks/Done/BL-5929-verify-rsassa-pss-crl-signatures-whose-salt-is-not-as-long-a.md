---
id: BL-5929
title: Verify RSASSA-PSS CRL signatures whose salt is not as long as the hash or whose MGF1 hash differs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateRevocationList.cs, Curl.Networking.UnitLibrary/RsaPssSignatureParameters.cs, Curl.Networking.UnitLibrary/CertificateRevocationListFile.cs, Curl.Networking.UnitTests/RsaPssSignatureParametersTests.cs, Curl.Networking.UnitTests/Fakes/TestRevocationList.cs, Curl.Networking.UnitTests/Fakes/TestRsaPssSignature.cs, Curl.Networking.UnitTests/IntermediateOnlyProxyCacertTests.cs, Curl.Networking.UnitTests/CertificateRevocationListFileTests.cs, Curl.Cryptography.UnitLibrary/RsaPssVerification.cs, Curl.Cryptography.UnitLibrary/CLAUDE.md, Curl.Cryptography.UnitTests/RsaPssVerificationTests.cs]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5929 — Verify RSASSA-PSS CRL signatures whose salt is not as long as the hash or whose MGF1 hash differs

## Goal

Curl answers a revocation list signed with RSASSA-PSS whose salt length is not the hash length, or whose MGF1 hash differs from the message hash, as real curl does.

## Context

BL-5878 (ADR-1364) verifies RSASSA-PSS lists through `RSASignaturePadding.Pss`, so `RsaPssSignatureParameters` admits only a salt as long as the hash and MGF1 over the same hash; any other list leaves the status "unknown", which `--ssl-revoke-best-effort` tolerates. `openssl ca -gencrl` signs PSS with the longest salt unless told otherwise, so such lists exist. Measure curl 8.22.0 Schannel below an intermediate `--cacert` anchor, and the OpenSSL build with `--crlfile`, with a long-salt list that names and one that does not name the server's certificate. The base class library has no other salt length, so verification needs a hand-built EMSA-PSS check (RFC 8017, 9.1.2); widen `touches` if it goes in a cryptography library.

## Acceptance criteria

- [x] Unit tests pin that a list with a salt length other than the hash length, and one with another MGF1 hash, verify or fail as measured, and that a certificate such a list revokes is refused where real curl refuses it.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured 2026-11-24, lists signed by `openssl ca -gencrl` (SHA-256, RSA 2048), each against a certificate the list names and one it does not. curl 8.22.0 Schannel (intermediate `--cacert` anchor, loopback distribution point): salt 222 or 0 answers revoked / accepted; MGF1 over SHA-1 or SHA-384, a salt other than the one named, and a corrupt signature all leave "revocation status is unknown" (best effort accepts). curl 8.18.0 OpenSSL 3.5.5 (`--crlfile`, WSL): salt 222 or 0 and another MGF1 hash verify; a salt other than the one named and a corrupt signature are `CRL signature failure (8)`.
- Plan and result (ADR-1369): hand-built `Curl.Cryptography.RsaPssVerification` (exact salt length, separate MGF1 hash); `RsaPssSignatureParameters` is now a record read from the DER; `IsSignedBy` admits another MGF1 hash only for `--crlfile`. No project reference added: Networking already uses Curl.Cryptography through Curl.Tls.
- Curl debug build beside the Schannel measurement: all 24 answers (6 lists, 2 certificates, strict and `--ssl-revoke-best-effort`) match. The OpenSSL answers are pinned by unit tests (Curl on Windows ignores `--crlfile`).
- `touches` widened (no other task in Doing names these): the new Cryptography files and its CLAUDE.md, `CertificateRevocationListFile.cs`, and the Networking test files listed.
- Verification: `dotnet build` clean, fast tests green, `Measure-CodeQuality.ps1 -Library Curl.Cryptography.UnitLibrary,Curl.Networking.UnitLibrary` reports 0 failing members.

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: f5c9856a 3d836d5e.
- 2026-11-24: Doing -> Done. RSASSA-PSS CRLs with any named salt length verify as curl does; another MGF1 hash verifies only for --crlfile (ADR-1369)
