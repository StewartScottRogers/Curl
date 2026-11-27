---
id: BL-6054
title: Measure and match the OpenSSL build's rfc822Name constraints starting with @, empty, and with a differently cased local part
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameConstraints.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6054 — Measure and match the OpenSSL build's rfc822Name constraints starting with @, empty, and with a differently cased local part

## Goal

Below a `--cacert` anchor, the OpenSSL build judges an rfc822Name constraint starting `@`, an empty one, and a mailbox constraint whose local part differs from the name's only in case, as OpenSSL does.

## Context

Found in BL-6046 (ADR-1437), which changed the Schannel build only. `CertificateNameConstraints.EmailMatches` compares a constraint holding `@` for equality ignoring case, and an empty one as a host. OpenSSL's `nc_email` (`crypto/x509/v3_ncons.c`) is expected to differ (an `@host` constraint matching every local part at that host, the local part compared with case), but none of this is measured. Measure with `openssl verify` (Git for Windows ships OpenSSL 3.5) or the OpenSSL build of curl, as excluded and as permitted subtrees.

## Acceptance criteria

- [x] An ADR records OpenSSL's measured answers for excluded and permitted rfc822Name constraints `@evil.test`, empty, and `u@evil.test` against `u@evil.test` and `U@evil.test`.
- [x] Unit tests in `Curl.Networking.UnitTests` pin the OpenSSL build's answers to match them, and the Schannel build's answers are unchanged.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured with `openssl verify` 3.5.7 (Git for Windows); table and decision in ADR-1448.
- Also matched, from the same measurement: an rfc822Name with no `@` under an rfc822Name constraint is
  unsupported name syntax (verify result 53), and the host is what follows the last `@` of each side.
- The Schannel build is unchanged; its existing tests pass unchanged. The empty-constraint tests stay
  excluded on macOS, as before. Quality: Curl.Networking.UnitLibrary 100% line and branch, 0 failing.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 86611341 4c860425 3d836d5e.
- 2026-11-26: Doing -> Done. The OpenSSL build matches rfc822Name constraints as OpenSSL's nc_email does (ADR-1448).
