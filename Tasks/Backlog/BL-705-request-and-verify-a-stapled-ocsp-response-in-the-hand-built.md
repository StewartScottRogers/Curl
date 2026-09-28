---
id: BL-705
title: Request and verify a stapled OCSP response in the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-699, BL-703]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-705 — Request and verify a stapled OCSP response in the hand-built TLS client

## Goal

The hand-built TLS client (1.3 and 1.2) sends `status_request`, receives the stapled OCSP response (the TLS 1.3 Certificate entry extension or the TLS 1.2 CertificateStatus message), and verifies it (RFC 6960: responder signature by the issuer or a delegated responder, `certID` match, `thisUpdate`/`nextUpdate` against `TimeProvider`, status good/revoked/unknown), reporting the outcome so `--cert-status` can fail with exit 91 `CURLE_SSL_INVALIDCERTSTATUS` as curl does.

## Context

- curl: `--cert-status` "Verify the status of the server certificate by using the Certificate Status Request (aka. OCSP stapling) TLS extension." (https://curl.se/docs/manpage.html, checked 2026-09-28). Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): honoured on every platform. Wiring and messages: BL-610.
- Design: BL-695's ADR. Builds on BL-699 and BL-703. OCSP response parsing with `System.Formats.Asn1`; signatures with the BCL's `RSA`/`ECDsa` (Ed25519 from `Curl.Cryptography.UnitLibrary`).
- Test data: responses generated in the test with the BCL (`CertificateRequest` for a test CA and responder, the OCSP structures encoded with `AsnWriter`).

## Acceptance criteria

- [ ] `Curl.Tls.UnitTests` verify good, revoked and unknown responses, and reject a missing response, a bad signature, a response for another certificate, an expired `nextUpdate` and an unauthorised responder, each with the typed outcome BL-610 maps to curl's messages, over TLS 1.3 and TLS 1.2 against the in-memory servers.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
