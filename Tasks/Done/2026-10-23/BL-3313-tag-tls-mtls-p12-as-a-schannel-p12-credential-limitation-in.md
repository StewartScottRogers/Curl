---
id: BL-3313
title: Tag tls-mtls-p12 as a Schannel P12 credential limitation in the differential runner
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.Differential.UnitLibrary/ReferenceBuildLimitation.cs, Curl.Conformance.Differential.UnitLibrary/Cases/CliTls/CliTlsCases.json]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3313 — Tag tls-mtls-p12 as a Schannel P12 credential limitation in the differential runner

## Goal

The CLI TLS differential runner no longer reports `tls-mtls-p12` as a difference on a Windows build whose Schannel refuses every P12 client credential (ADR-0794).

## Context

BL-3290 measured that curl 8.22.0 (ucrt64) and curl 8.21.0 (System32) on Windows 11 26H2 build 26300 fail every P12 client certificate with exit 35 `SEC_E_UNKNOWN_CREDENTIALS`, and ADR-0794 decides Curl keeps presenting it. Add a `ReferenceBuildLimitation` member for this (its summary says the platform Schannel, not only the reference build, refuses the credential) and tag `tls-mtls-p12` in `Cases/CliTls/CliTlsCases.json` with it, as `NoHttp2` cases are tagged. Leave `tls-mtls-p12-badpw` untagged: it still matches (exit 58).

## Acceptance criteria

- [x] `ReferenceBuildLimitation` has a documented member for the Schannel P12 credential refusal and `tls-mtls-p12` carries it in `CliTlsCases.json`.
- [x] The build is clean and the fast tests pass.

## Notes

- Member named SchannelRefusesP12Credential; tls-mtls-p12-badpw left untagged. Build clean; Differential.UnitTests 590 passed; quality run 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 1abf7f6d c46ca32a.
- 2026-10-23: Doing -> Done. tls-mtls-p12 tagged SchannelRefusesP12Credential in the differential runner
