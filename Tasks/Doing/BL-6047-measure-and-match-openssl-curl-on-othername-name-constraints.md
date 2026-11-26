---
id: BL-6047
title: Measure and match OpenSSL curl on otherName name constraints below an intermediate anchor
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameConstraints.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6047 — Measure and match OpenSSL curl on otherName name constraints below an intermediate anchor

## Goal

The OpenSSL build answers an `otherName` name constraint below a non-self-signed `--cacert` anchor as OpenSSL curl does.

## Context

Found in BL-6011 (ADR-1430), which changed the Schannel build only: `CertificateNameConstraints.ReadOpenSslNames` lists no `otherName`, so where the port's own check decides (a partial-chain anchor) an `otherName` constraint is not evaluated. Not measured against OpenSSL curl (WSL Ubuntu). OpenSSL's `nc_match_single` handles `GEN_OTHERNAME` for SmtpUTF8Mailbox and may report an unsupported constraint type for others. Measure a UPN and a SmtpUTF8Mailbox constraint, excluded and permitted.

## Acceptance criteria

- [ ] The measured OpenSSL curl answers are recorded in an ADR, and the OpenSSL build gives the same exit code and message for each.
- [ ] Unit tests in `Curl.Networking.UnitTests` pin them.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: f20e1cc9 ec79c5dc 3d836d5e.
