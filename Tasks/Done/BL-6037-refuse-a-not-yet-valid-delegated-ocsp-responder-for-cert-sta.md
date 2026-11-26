---
id: BL-6037
title: Refuse a not-yet-valid delegated OCSP responder for --cert-status as curl's OpenSSL build does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary/OcspStapleVerifier.cs, Curl.Tls.UnitLibrary/OcspCertificateFields.cs, Curl.Tls.UnitTests/OcspStapleVerifierTests.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorOcsp.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.AnchorOcsp.cs, Curl.Tls.UnitTests/OcspTestPki.cs, Curl.Tls.UnitLibrary/CLAUDE.md, Documentation/Planning/Decisions/ADR-1423-an-expired-delegated-ocsp-responder-is-not-authorised.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6037 — Refuse a not-yet-valid delegated OCSP responder for --cert-status as curl's OpenSSL build does

## Goal

Under `--cert-status`, an answer signed by a delegated OCSP responder that is not yet valid is
refused as curl's OpenSSL build refuses it, while the Windows check below an anchor still accepts it.

## Context

BL-6008 (ADR-1423) made `OcspStapleVerifier` refuse a delegated responder past its `notAfter`.
A responder before its `notBefore` is still accepted everywhere, because curl 8.22.0 Schannel
accepts it below an anchor (fitness finding STD-215). OpenSSL's `OCSP_basic_verify` builds the
responder's chain with `X509_verify_cert`, which also refuses "certificate is not yet valid", so
`--cert-status` against curl's OpenSSL build is expected to differ. Measure it first. Callers of
`OcspStapleVerifier.Verify`: `ServerCertificateVerification` (stapled status and the anchor
check), `HandBuiltCertificateVerifier` and both TLS handshakes.

## Acceptance criteria

- [x] The Notes record what curl's OpenSSL build prints and exits for `--cert-status` with a stapled "good" signed by a not-yet-valid delegated responder.
- [x] A unit test in `Curl.Tls.UnitTests` pins `--cert-status`'s answer as measured.
- [x] The check below a `--cacert` anchor on Windows still accepts a not-yet-valid responder, and the existing tests pass.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured 2026-11-26, curl 8.18.0 on OpenSSL 3.5.5 (WSL Ubuntu), `--cert-status --cacert ca.pem`
  against `openssl s_server -status_file ... -cert_chain ca.pem` on 127.0.0.1, TLS 1.3 and 1.2, a
  stapled "good" from a delegated responder (EKU OCSPSigning, signed by the leaf's issuer):
  - `notBefore` 2 days ahead, and 120 seconds ahead: exit 91,
    `curl: (91) OCSP response verification failed` (`-v`: `* OCSP response verification failed`).
  - `notAfter` 120 seconds ago: the same. Valid responder: exit 0,
    `* SSL certificate status: good (0)`.
  - WSL `openssl s_server` was used because `Record-CurlExchange.ps1` runs the Windows curl,
    which has no `--cert-status`; the OpenSSL build is the one being measured.
- Fix (ADR-1432): `OcspStapleVerifier.Verify` (what both handshakes run for `--cert-status`) now
  refuses a delegated responder before its `notBefore`, no leeway. The two Windows revocation
  checks (stapled status, below-anchor query) call the new
  `VerifyForSchannelRevocationCheck`, which still accepts one.
- `touches` widened to the stapled-status caller, the anchor tests, `OcspTestPki` (its default
  responder was valid only from yesterday, after the tests' fixed time), the library's CLAUDE.md
  and ADR-1423's status line. No other task in Doing on `origin/work/dark-factory` named them.
- Tests: `ADelegatedResponderIsAuthorisedForCertStatusOnlyFromItsNotBefore`,
  `TheSchannelRevocationCheckAcceptsANotYetValidResponderButNoExpiredOne`,
  `VerifyPeer_OcspGoodFromANotYetValidDelegatedResponder_Accepts`. Build clean, fast tests green,
  `Measure-CodeQuality.ps1` for Curl.Tls and Curl.Networking: 0 failing members.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: bb26c3f6 7a5be3ea 20fe6bb3 3d836d5e.
- 2026-11-26: Doing -> Done. --cert-status now refuses a not-yet-valid delegated OCSP responder (exit 91); the Windows revocation checks still accept it
