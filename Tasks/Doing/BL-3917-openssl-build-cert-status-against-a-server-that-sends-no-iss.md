---
id: BL-3917
title: OpenSSL build: --cert-status against a server that sends no issuer reports 'Error computing OCSP ID' as curl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateStatusFailureMessages.cs, Curl.Tls.UnitLibrary/OcspStapleVerifier.cs, Curl.Networking.UnitTests/OcspIssuerNotSentTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3917 — OpenSSL build: --cert-status against a server that sends no issuer reports 'Error computing OCSP ID' as curl does

## Goal

With `--cert-status` and `--cacert` (no `-k`) against a server that sends only its own certificate, the OpenSSL build of Curl exits 91 with `Error computing OCSP ID`, as curl 8.18.0 on OpenSSL 3.5.5 does, where it now prints `Error finding issuer certificate`.

## Context

Found by BL-3696 (CGR-167), measured in WSL: `openssl s_server -accept <p> -cert g.crt -key srv.key -WWW -status_file g.ocsp` (no `-cert_chain`, so the issuer `rca.crt` is not sent; `g.ocsp` is a good response signed by the root), then `curl -sS --cacert rca.crt --cert-status https://localhost:<p>/index.txt`: curl exits 91 `curl: (91) Error computing OCSP ID`; Curl (linux-x64 build) exits 91 `curl: (91) Error finding issuer certificate`. Under `-k` both print `OCSP response verification failed` (ADR-0862). curl's `verifystatus()` in `lib/vtls/openssl.c` looks for the issuer in the verified chain (`SSL_get0_verified_chain`), which holds the `--cacert` root, so it finds one, and `OCSP_cert_to_id` then fails; check the exact path in curl 8.18 source and re-measure with curl 8.22.0 if one is to hand. `OcspStapleVerifier` (ADR-0191) and `CertificateStatusFailureMessages` are where the outcome and its text live; the Schannel build ignores `--cert-status` (ADR-0795).

## Acceptance criteria

- [ ] The repro above gives the same exit code and stderr text through real curl and through Curl's OpenSSL build; the commands and both results are recorded in Notes.
- [ ] A focused unit test in `Curl.Networking.UnitTests` fails before the fix and passes after it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: e22c8789 5720596e 779790c0.
