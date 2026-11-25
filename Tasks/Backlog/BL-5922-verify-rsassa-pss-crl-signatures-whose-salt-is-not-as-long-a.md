---
id: BL-5922
title: Verify RSASSA-PSS CRL signatures whose salt is not as long as the hash or whose MGF1 hash differs
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateRevocationList.cs, Curl.Networking.UnitLibrary/RsaPssSignatureParameters.cs, Curl.Networking.UnitTests/RsaPssSignatureParametersTests.cs, Curl.Networking.UnitTests/Fakes/TestRevocationList.cs]
requirement: none
created: 2026-11-24
completed:
---
# BL-5922 — Verify RSASSA-PSS CRL signatures whose salt is not as long as the hash or whose MGF1 hash differs

## Goal

Curl answers a revocation list signed with RSASSA-PSS whose salt length is not the hash length, or whose MGF1 hash differs from the message hash, as real curl does.

## Context

BL-5878 (ADR-1357) verifies RSASSA-PSS lists through `RSASignaturePadding.Pss`, so `RsaPssSignatureParameters` admits only a salt as long as the hash and MGF1 over the same hash; any other list leaves the status "unknown", which `--ssl-revoke-best-effort` tolerates. `openssl ca -gencrl` signs PSS with the longest salt unless told otherwise, so such lists exist. Measure curl 8.22.0 Schannel below an intermediate `--cacert` anchor, and the OpenSSL build with `--crlfile`, with a long-salt list that names and one that does not name the server's certificate. The base class library has no other salt length, so verification needs a hand-built EMSA-PSS check (RFC 8017, 9.1.2); widen `touches` if it goes in a cryptography library.

## Acceptance criteria

- [ ] Unit tests pin that a list with a salt length other than the hash length, and one with another MGF1 hash, verify or fail as measured, and that a certificate such a list revokes is refused where real curl refuses it.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
