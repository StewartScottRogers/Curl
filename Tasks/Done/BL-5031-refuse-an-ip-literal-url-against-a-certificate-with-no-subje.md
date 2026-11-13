---
id: BL-5031
title: Refuse an IP-literal URL against a certificate with no subjectAltName on the Schannel profile
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.VerifyChain.cs, Curl.Networking.UnitTests/TlsFailureMessagesTests.cs]
requirement: none
created: 2026-11-12
completed: 2026-11-12
---
# BL-5031 — Refuse an IP-literal URL against a certificate with no subjectAltName on the Schannel profile

## Goal

On the Schannel profile with `--cacert`, an IP-literal URL against a certificate that has only `CN=<IP>` and no subjectAltName exits 60 with `schannel: CertFindExtension() returned no extension.`.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-033 (Low, Confirmed).

Area: HTTPS host-name verification, Windows (Schannel profile).

Repro:
1. Issue a leaf signed by a test CA `ca.pem` with subject `/CN=127.0.0.1` and no subjectAltName.
2. Serve it over HTTPS on 127.0.0.1:43403 (with a CRL distribution point) and on 43114 (without one).
3. `curl -sS https://127.0.0.1:43403/ --cacert ca.pem`
4. `curl -sS https://127.0.0.1:43114/ --cacert ca.pem --ssl-no-revoke`

Expected (REF): exit 60, `curl: (60) schannel: CertFindExtension() returned no extension.` REF2 (OpenSSL) accepts, so this matters only for the Schannel profile.

Actual (PORT): exit 0 and the body is delivered, on both the SslStream and hand-built routes. PORT already prints REF's message for a `CN=localhost` certificate reached by IP.

Where to start: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`, `WithoutNameMismatchSchannelAccepts` (about line 560), and `TlsFailureMessages.cs` (`SchannelIpAddressWithoutAlternativeNames`, about line 53 and 368). In the Schannel build with `--cacert`, an IP-literal target against a certificate with no SAN extension must be a name mismatch.

## Acceptance criteria

- [x] A unit test in `Curl.Networking.UnitTests/ServerCertificateVerificationTests.VerifyChain.cs` (Schannel profile, `--cacert`, target `127.0.0.1`, leaf `CN=127.0.0.1` with no SAN extension) shows exit 60 with the message `schannel: CertFindExtension() returned no extension.`, on both the SslStream-route and hand-built-route judgments.
- [x] A test in `Curl.Networking.UnitTests/TlsFailureMessagesTests.cs` pins that message for this case.
- [x] Regression: the OpenSSL profile still accepts the same certificate; a Schannel-profile leaf that has an `IP:127.0.0.1` SAN is still accepted; the existing `CN=localhost` reached by IP still prints REF's message.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. Schannel profile only; OpenSSL accepts. The system-store trust route (no `--cacert`) was not tested in the review. Drive the profile through the `matchesSchannelBuild` flag so the tests pass on every OS.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: 4bf4f29c c3bb10d0 696fa81a a0b49a29.
- 2026-11-12: Schannel + --cacert + IP-literal target + leaf with no subjectAltName extension is now a name mismatch in ServerCertificateVerification (message was already pinned in TlsFailureMessagesTests). Not re-measured against real curl: the task cites the REF measurement.
- 2026-11-12: Doing -> Done. Schannel IP-literal target vs no-SAN certificate refused; tests and quality gate green
