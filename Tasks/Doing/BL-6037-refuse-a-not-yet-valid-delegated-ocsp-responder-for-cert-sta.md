---
id: BL-6037
title: Refuse a not-yet-valid delegated OCSP responder for --cert-status as curl's OpenSSL build does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Tls.UnitLibrary/OcspStapleVerifier.cs, Curl.Tls.UnitLibrary/OcspCertificateFields.cs, Curl.Tls.UnitTests/OcspStapleVerifierTests.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorOcsp.cs]
requirement: none
created: 2026-11-26
completed:
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

- [ ] The Notes record what curl's OpenSSL build prints and exits for `--cert-status` with a stapled "good" signed by a not-yet-valid delegated responder.
- [ ] A unit test in `Curl.Tls.UnitTests` pins `--cert-status`'s answer as measured.
- [ ] The check below a `--cacert` anchor on Windows still accepts a not-yet-valid responder, and the existing tests pass.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: bb26c3f6 7a5be3ea 20fe6bb3 3d836d5e.
