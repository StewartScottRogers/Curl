---
id: BL-5428
title: Hand the connect's diagnostic log and remaining time to the below-anchor CRL fetch
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5428 — Hand the connect's diagnostic log and remaining time to the below-anchor CRL fetch

## Goal

A below-anchor CRL fetch writes its failures to the run's `tls` diagnostic log and is bounded by what is left of the connect's limit.

## Context

BL-5422 gave `ServerCertificateVerification` the init properties `RevocationDiagnosticLog` and `RevocationFetchBudget` (default 5 s, a total per verification); the two providers construct it and still leave both at their defaults, so nothing reaches `-v`/`--log-level` and the budget ignores `--connect-timeout` and `-m` (ADR-0117).

## Acceptance criteria

- [ ] Both providers set `RevocationDiagnosticLog` from the target's diagnostic log; a unit test shows a failed fetch reaches it as a `tls` warning.
- [ ] Both providers set `RevocationFetchBudget` from the connect's remaining time; a unit test shows a stalled fetch does not outlast the connect limit.
- [ ] Curl.Networking.UnitLibrary keeps its quality gates.

## Notes

## Log

- 2026-11-18: Created.
