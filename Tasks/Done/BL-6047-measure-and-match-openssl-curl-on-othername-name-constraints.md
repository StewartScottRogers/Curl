---
id: BL-6047
title: Measure and match OpenSSL curl on otherName name constraints below an intermediate anchor
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/CertificateNameConstraints.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NameConstraints.cs, Curl.Networking.UnitLibrary/OpenSslVerifyResult.cs, Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitLibrary/OpenSslSmtpUtf8Mailbox.cs, Curl.Networking.UnitLibrary/PunycodeLabel.cs, Curl.Networking.UnitTests/PunycodeLabelTests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6047 — Measure and match OpenSSL curl on otherName name constraints below an intermediate anchor

## Goal

The OpenSSL build answers an `otherName` name constraint below a non-self-signed `--cacert` anchor as OpenSSL curl does.

## Context

Found in BL-6011 (ADR-1430), which changed the Schannel build only: `CertificateNameConstraints.ReadOpenSslNames` lists no `otherName`, so where the port's own check decides (a partial-chain anchor) an `otherName` constraint is not evaluated. Not measured against OpenSSL curl (WSL Ubuntu). OpenSSL's `nc_match_single` handles `GEN_OTHERNAME` for SmtpUTF8Mailbox and may report an unsupported constraint type for others. Measure a UPN and a SmtpUTF8Mailbox constraint, excluded and permitted.

## Acceptance criteria

- [x] The measured OpenSSL curl answers are recorded in an ADR, and the OpenSSL build gives the same exit code and message for each.
- [x] Unit tests in `Curl.Networking.UnitTests` pin them.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Measured curl 8.18.0 / OpenSSL 3.5.5 (WSL Ubuntu) with a bash script run in WSL (openssl `s_server`, throwaway certificates). Answers are in ADR-1441: `otherName` under a same-type-id constraint is 51; a SmtpUTF8Mailbox `otherName` constraint is ignored; rfc822Name constraints apply to a SmtpUTF8Mailbox name (47, 48, 53, 1).
- `touches` widened with `OpenSslVerifyResult.cs`, `TlsFailureMessages.cs`, and new files `OpenSslSmtpUtf8Mailbox.cs`, `PunycodeLabel.cs`, `PunycodeLabelTests.cs`: the new verify codes need mapping and text, and the mailbox comparison is its own type. No other task in Doing on `origin/work/dark-factory` names any of them.
- Removed `FindViolation_OpenSslBuildExcludedOtherName_IsLeftToTheChainEngine`, which pinned the unmeasured behaviour this task replaces.
- Platform: the new tests drive the OpenSSL build through the same fakes as the existing cross-platform ones, with no OS-specific paths or text.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary` 100% line, 100% branch, 0 failing members.
- Follow-up: BL-6059 (root anchor, where the chain engine decides).

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: f20e1cc9 ec79c5dc 3d836d5e.
- 2026-11-26: Doing -> Done. The OpenSSL build answers otherName name constraints below an intermediate anchor as OpenSSL curl does (ADR-1441).
