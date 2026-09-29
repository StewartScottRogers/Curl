---
id: BL-980
title: Take the SHA-3 hash for tls-server-end-point bindings of a SHA-3-signed server certificate
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-965]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-980 — Take the SHA-3 hash for tls-server-end-point bindings of a SHA-3-signed server certificate

## Goal

Hand-built Negotiate over HTTPS does what curl 8.18.0 (OpenSSL, MIT) is measured doing when the server certificate is signed with RSA or ECDSA over SHA3-224, SHA3-256, SHA3-384 or SHA3-512, instead of failing with BL-965's unknown-OID exit 91.

## Context

- BL-965's `TlsServerEndPointChannelBindings.Of` (Curl.Authentication.UnitLibrary) maps signature OIDs to RFC 5929 hashes and throws `HttpAuthenticationFailedException` (exit 91, "Unable to find digest NID for certificate signature algorithm") for any OID outside its table and its PSS/EdDSA set (ADR-0234, amendment BL-965).
- OpenSSL pairs the SHA-3 signature OIDs (`id-rsassa-pkcs1-v1_5-with-sha3-*` 2.16.840.1.101.3.4.3.13-16, `id-ecdsa-with-sha3-*` 2.16.840.1.101.3.4.3.9-12) with a real digest, so curl presumably sends bindings hashed with that SHA-3. `SHA3_256.IsSupported` is false on macOS, so the hash must be hand-built (Keccak) in this library to pass on all three platforms.
- Measure as BL-965 did (ADR-0234 amendment): `openssl req -newkey rsa:2048 -sha3-256` certificate served by `openssl s_server -HTTP` in WSL to curl 8.18.0.

## Acceptance criteria

- [ ] The measurement for an RSA SHA3-256 certificate is recorded in ADR-0234 (amended again) or a new ADR.
- [ ] `Curl.Authentication.UnitTests` tests pin a hand-built SHA-3 to NIST examples and the bindings for each SHA-3 signature OID curl is measured to accept.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-965.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
