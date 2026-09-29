---
id: BL-965
title: Match curl's Negotiate over HTTPS when the server certificate's signature names no RFC 5929 hash
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-915]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-965 — Match curl's Negotiate over HTTPS when the server certificate's signature names no RFC 5929 hash

## Goal

Hand-built Negotiate over HTTPS does what curl 8.18.0 (OpenSSL, MIT) is measured doing when the server certificate is signed with RSASSA-PSS, Ed25519, Ed448 or SHA-224, instead of ADR-0234's provisional "send no bindings".

## Context

- BL-915's `TlsServerEndPointChannelBindings.Of` (Curl.Authentication.UnitLibrary) knows the RSA PKCS #1, ECDSA and DSA signature OIDs with MD5, SHA-1, SHA-256, SHA-384 and SHA-512, and gives `null` (no bindings) for any other (ADR-0234).
- curl's OpenSSL `Curl_ssl_get_channel_binding` looks the digest up from the signature's NID (`OBJ_find_sigid_algs`, `EVP_get_digestbynid`) and, as read from source, fails when it finds none; the Negotiate step may then fail the transfer. SHA-224 is a real digest there, and the BCL has no SHA-224 (hand-build it in this library if curl uses it). For RSASSA-PSS the digest sits in the algorithm parameters.
- Measure with `Record-CurlExchange.ps1` (extend it to serve HTTPS with a chosen certificate if needed) on curl 8.18.0 OpenSSL + MIT (WSL) before pinning the exit code and `-v` text.

## Acceptance criteria

- [ ] The measurement for a PSS and an Ed25519 certificate is recorded in ADR-0234 (amended) or a new ADR.
- [ ] `Curl.Authentication.UnitTests` tests pin the measured behaviour for each of those certificates, and SHA-224's bindings if curl sends them.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-915.

## Log

- 2026-09-29: Created.
