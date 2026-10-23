---
id: BL-3215
title: Reject a revoked server certificate on Windows with exit 35 as the Schannel build does
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitLibrary/HandBuiltCertificateVerifier.cs, Curl.Networking.UnitTests/ServerCertificateRevocationTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3215 — Reject a revoked server certificate on Windows with exit 35 as the Schannel build does

## Goal

On Windows, a server whose certificate chain (from the system store, not only a `--cacert` chain) contains a revoked certificate fails with exit 35 and the Schannel build's `CRYPT_E_REVOKED` text, unless `--ssl-no-revoke` (or `--ssl-revoke-best-effort` with an unreachable responder) says otherwise.

## Context

Stewart reported on 2026-10-22 (prioritised findings list, item "Other decisions": revocation). Security. Measured by the tester: `curl -sS https://revoked.badssl.com/` - Curl prints the page (exit 0, 4.4 s); curl 8.22.0 Schannel fails: `curl: (35) schannel: next InitializeSecurityContext failed: CRYPT_E_REVOKED (0x80092010) - The certificate is revoked.` ADR-0321 covers revocation for a `--cacert` chain; the default-store path evidently does not check it. Find why, and check revocation the way Schannel does (CRL distribution points and OCSP, online, end-entity and intermediates as Schannel's default flags do); keep `--ssl-no-revoke` and `--ssl-revoke-best-effort` (ADR-0124). Linux and macOS OpenSSL builds do not check revocation by default; leave them as they are. Tests must not reach the network: use a fake revocation source or loopback CRL/OCSP responder with generated certificates.

Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only and pass on Windows, Linux and macOS.

## Acceptance criteria

- [ ] `Curl.Networking.UnitTests/ServerCertificateRevocationTests.cs` (Windows `[OSCondition]`) shows a default-store chain with a revoked leaf fails with exit 35 and the Schannel text above, and a revoked intermediate the same.
- [ ] The same chain with `--ssl-no-revoke` succeeds; with `--ssl-revoke-best-effort` and an unreachable responder it succeeds, and with a reachable responder saying revoked it fails.
- [ ] A test pins that Linux and macOS (OpenSSL build behaviour) do not check revocation by default.
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members; no test touches the network.

## Notes

## Log

- 2026-10-22: Created.
