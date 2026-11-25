---
id: BL-5926
title: Report a host name mismatch before certificate rejected (28) in the Linux build as curl 8.18 does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-5917]
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.TrustedCertificate.cs, Curl.Networking.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-24
completed:
---
# BL-5926 — Report a host name mismatch before certificate rejected (28) in the Linux build as curl 8.18 does

## Goal

A rejected --cacert or --capath certificate with a host name the leaf does not carry exits 60 with the host name mismatch message, as curl 8.18.0 on OpenSSL does.

## Context

Found by BL-5917 (ADR-1363). ADR-1351 and VerifyChain_RejectedRootAndAnotherHostName_ReportsTheRejectionFirst pin certificate rejected (28) first, but WSL curl 8.18.0 (OpenSSL 3.5.5) with --cacert rejected.pem, or --capath, against https://other.test (via --resolve) or an IP address the certificate lacks printed "SSL: no alternative certificate subject name matches target ..." with %{ssl_verify_result} 1. Re-measure, then fix the order in ServerCertificateVerification.Refusal and the CLAUDE.md sentence if it says otherwise; record the correction to ADR-1351 in a new ADR.

## Acceptance criteria

- [ ] With a rejected root (--cacert or --capath) and a mismatched host name the failure is curl's host name mismatch message, exit 60.
- [ ] Focused unit tests in Curl.Networking.UnitTests pin it, and dotnet build and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 29c5d753 645f4828.
