---
id: BL-5917
title: Make the Linux build read TRUSTED CERTIFICATE blocks in --capath files as OpenSSL does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-5879]
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.TrustedCertificate.cs, Curl.Networking.UnitLibrary/OpenSslCaFile.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-24
completed: 2026-11-24
---
# BL-5917 — Make the Linux build read TRUSTED CERTIFICATE blocks in --capath files as OpenSSL does

## Goal

The OpenSSL build trusts, or rejects with exit 60 `certificate rejected (28)`, a certificate held as a `TRUSTED CERTIFICATE` block in a `--capath` hashed file, as OpenSSL curl does.

## Context

Follow-up from BL-5879 (ADR-1351), which honours these settings in `--cacert` only. `ImportHashedCertificates` in `ServerCertificateVerification.CaCertificates.cs` imports `CERTIFICATE` blocks alone, so a `<hash>.0` file in the trusted format adds nothing; OpenSSL's `by_dir` reads it with `PEM_read_bio_X509_AUX`. `OpenSslCaFile` already parses the format. Measure real curl (WSL, curl 8.18 OpenSSL) before pinning behaviour, a rejected `--capath` certificate beside a `--cacert` file included.

## Acceptance criteria

- [x] A `--capath` hashed file holding a root trusted for `serverAuth` as a `TRUSTED CERTIFICATE` block verifies a server it signed (exit 0).
- [x] The same file rejecting `serverAuth` gives exit 60 `certificate rejected (28)`.
- [x] Focused unit tests in `Curl.Networking.UnitTests` pin both, and `dotnet build` and the fast tests pass.

## Notes

- Done; ADR-1359 records the behaviour. Added `OpenSslCaFile.cs`, `ServerCertificateVerification.cs` and the library's `CLAUDE.md` to `touches`: the parser, the anchor reading and the refusal live there, and no task in Doing named them.
- Measured (WSL curl 8.18.0, OpenSSL 3.5.5): rejected root in `--capath` exit 60 (28), trusted exit 0; first copy wins (`.0` before `.1`, earlier directory first); the `--cacert` copy wins over the `--capath` one; rejected `--capath` root beside an unrelated `--cacert` still (28); undecodable first block adds nothing.
- Choice: without `--cacert`, a rejected certificate that roots a fault-free system store chain is not refused, as curl's default CA file copy would win.
- Quality: the full `-Library` run found two methods over the complexity gate; both split, then rescored with `-SkipTestRun` over a fresh Networking.UnitTests coverage run, where none of the changed members fail.
- Follow-ups: BL-5926 (host name mismatch is reported before (28), contrary to ADR-1351), BL-5927 (`X509 CERTIFICATE` blocks).

## Log

- 2026-11-24: Created.
- 2026-11-24: Backlog -> Doing. Criteria at claim: 35866a3d fba95e32 ea3ceb06.
- 2026-11-24: Doing -> Done. --capath TRUSTED CERTIFICATE blocks honoured; build and fast tests green
