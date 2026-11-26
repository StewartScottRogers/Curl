---
id: BL-6048
title: Measure and match the OpenSSL build's rfc822Name constraints starting with @, empty, and with a differently cased local part
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameConstraints.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6048 — Measure and match the OpenSSL build's rfc822Name constraints starting with @, empty, and with a differently cased local part

## Goal

Below a `--cacert` anchor, the OpenSSL build judges an rfc822Name constraint starting `@`, an empty one, and a mailbox constraint whose local part differs from the name's only in case, as OpenSSL does.

## Context

Found in BL-6046 (ADR-1431), which changed the Schannel build only. `CertificateNameConstraints.EmailMatches` compares a constraint holding `@` for equality ignoring case, and an empty one as a host. OpenSSL's `nc_email` (`crypto/x509/v3_ncons.c`) is expected to differ (an `@host` constraint matching every local part at that host, the local part compared with case), but none of this is measured. Measure with `openssl verify` (Git for Windows ships OpenSSL 3.5) or the OpenSSL build of curl, as excluded and as permitted subtrees.

## Acceptance criteria

- [ ] An ADR records OpenSSL's measured answers for excluded and permitted rfc822Name constraints `@evil.test`, empty, and `u@evil.test` against `u@evil.test` and `U@evil.test`.
- [ ] Unit tests in `Curl.Networking.UnitTests` pin the OpenSSL build's answers to match them, and the Schannel build's answers are unchanged.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
