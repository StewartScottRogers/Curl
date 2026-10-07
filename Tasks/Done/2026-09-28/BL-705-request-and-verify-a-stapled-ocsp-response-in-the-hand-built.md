---
id: BL-705
title: Request and verify a stapled OCSP response in the hand-built TLS client
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-699, BL-703]
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-705 — Request and verify a stapled OCSP response in the hand-built TLS client

## Goal

The hand-built TLS client (1.3 and 1.2) sends `status_request`, receives the stapled OCSP response (the TLS 1.3 Certificate entry extension or the TLS 1.2 CertificateStatus message), and verifies it (RFC 6960: responder signature by the issuer or a delegated responder, `certID` match, `thisUpdate`/`nextUpdate` against `TimeProvider`, status good/revoked/unknown), reporting the outcome so `--cert-status` can fail with exit 91 `CURLE_SSL_INVALIDCERTSTATUS` as curl does.

## Context

- curl: `--cert-status` "Verify the status of the server certificate by using the Certificate Status Request (aka. OCSP stapling) TLS extension." (https://curl.se/docs/manpage.html, checked 2026-09-28). Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): honoured on every platform. Wiring and messages: BL-610.
- Design: BL-695's ADR. Builds on BL-699 and BL-703. OCSP response parsing with `System.Formats.Asn1`; signatures with the BCL's `RSA`/`ECDsa` (Ed25519 from `Curl.Cryptography.UnitLibrary`).
- Test data: responses generated in the test with the BCL (`CertificateRequest` for a test CA and responder, the OCSP structures encoded with `AsnWriter`).

## Acceptance criteria

- [x] `Curl.Tls.UnitTests` verify good, revoked and unknown responses, and reject a missing response, a bad signature, a response for another certificate, an expired `nextUpdate` and an unauthorised responder, each with the typed outcome BL-610 maps to curl's messages, over TLS 1.3 and TLS 1.2 against the in-memory servers.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Design recorded in ADR-0173 (Decided by Claude under Stewart's delegation). ADR-0169 to
  ADR-0172 are taken on other lanes' branches, so this one is 0173.
- `touches` gained `Documentation/Planning/Decisions` for ADR-0173 and its index line; no
  task in Doing names it (checked 2026-09-28).
- `OcspStapleVerifier.Verify(response, chain, now)` returns `OcspStapleOutcome`
  (`OcspStapleStatus` + CRL reason / `responseStatus`), checking in curl's OpenSSL
  `verifystatus()` order: no response, malformed, unsuccessful, issuer not in chain,
  responder not found, signature, responder authority, `CertID`, revoked/unknown, then
  `thisUpdate`/`nextUpdate` with OpenSSL's 300 s leeway and no maximum age.
- Both handshakes: with `RequestOcspStatus` (new on TLS 1.3; `status_request` goes after
  `supported_groups`) and the settings' new `TimeProvider`, the check runs once the
  verifier accepts the chain; anything but good sends `bad_certificate_status_response`
  and sets `TlsHandshakeFailure.CertificateStatusRejection`. Chosen over completing the
  handshake then failing (OpenSSL's way) because the exit and message BL-610 produces are
  the same and no request is ever written.
- Two existing TLS 1.2 tests changed meaning: a server that echoes `status_request` but
  omits CertificateStatus now fails the status check with `NoResponse` (curl's "No OCSP
  response received"), and the hand-off test staples a real good response.
- Test servers (`Tls13TestServer`, `Tls12TestServer`) gained `IssuerCertificates` so the
  leaf can come with its CA; responses are written by `OcspResponseBuilder` with
  `AsnWriter`, certificates by `OcspTestPki` with the BCL's `CertificateRequest`.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary` 100% line, 100%
  branch, 694 members, 0 failing, worst CRAP 10. Curl.Tls.UnitTests 733 passed.
- Left to BL-610: the message text per outcome, and whether `--cert-status` resumes TLS
  1.2 sessions (a resumed session is not checked; `CertificateStatus` stays null).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. The hand-built TLS 1.3 and 1.2 clients ask for, verify and report a stapled OCSP response (OcspStapleVerifier, typed OcspStapleOutcome) for --cert-status's exit 91
