---
id: BL-5428
title: Hand the connect's diagnostic log and remaining time to the below-anchor CRL fetch
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SslStreamTlsProvider.cs, Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs, Curl.Networking.UnitLibrary/IHandshakeReportingTlsProvider.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs, Curl.Networking.UnitLibrary/TcpConnector.cs, Curl.Networking.UnitTests/IntermediateOnlyProxyCacertTests.cs]
requirement: none
created: 2026-11-18
completed: 2026-11-18
---
# BL-5428 — Hand the connect's diagnostic log and remaining time to the below-anchor CRL fetch

## Goal

A below-anchor CRL fetch writes its failures to the run's `tls` diagnostic log and is bounded by what is left of the connect's limit.

## Context

BL-5422 gave `ServerCertificateVerification` the init properties `RevocationDiagnosticLog` and `RevocationFetchBudget` (default 5 s, a total per verification); the two providers construct it and still leave both at their defaults, so nothing reaches `-v`/`--log-level` and the budget ignores `--connect-timeout` and `-m` (ADR-0117).

## Acceptance criteria

- [x] Both providers set `RevocationDiagnosticLog` from the target's diagnostic log; a unit test shows a failed fetch reaches it as a `tls` warning.
- [x] Both providers set `RevocationFetchBudget` from the connect's remaining time; a unit test shows a stalled fetch does not outlast the connect limit.
- [x] Curl.Networking.UnitLibrary keeps its quality gates.

## Notes

The providers are shared by every connect, so the log and limit cannot be set on them once: `IHandshakeReportingTlsProvider` gained a default overload taking the target's `IDiagnosticLog` (TcpConnector passes `target.DiagnosticLog`), and each provider builds a per-handshake `ServerCertificateVerification.ForHandshake(log, token)`. The remaining connect time is the handshake token, which TcpConnector cancels at the connect limit; `RevocationLimit` links it with `RevocationFetchBudget`, so the fetch ends at the lesser. Touches widened to the interface, TcpConnector, the verification partial and the test file (no other Doing task holds them). Tests: ForHandshake_* in IntermediateOnlyProxyCacertTests. Quality gate for Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-11-18: Created.
- 2026-11-18: Backlog -> Doing. Criteria at claim: 59b0f4ef 5dbbc2cb 9abf1d2b.
- 2026-11-18: Doing -> Done. Below-anchor CRL fetch now logs failures to the connect target's tls log and stops at the connect limit
