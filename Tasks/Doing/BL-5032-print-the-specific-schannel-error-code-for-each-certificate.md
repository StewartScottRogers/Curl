---
id: BL-5032
title: Print the specific schannel error code for each certificate-chain failure
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TlsFailureMessages.cs, Curl.Networking.UnitTests/TlsFailureMessagesTests.cs]
requirement: none
created: 2026-11-12
completed:
---
# BL-5032 — Print the specific schannel error code for each certificate-chain failure

## Goal

A certificate-chain failure on the Schannel profile prints `schannel: error 0x<CERT_TRUST_* value>` for the specific chain status, not always the untrusted-root text.

## Context

From the outside fitness review of 2026-11-12 (commit 2d2ad7f8b, `preview` curl-win-x64.zip; REF = curl 8.22.0 Schannel, REF2 = curl 8.18.0 OpenSSL), finding STD-034 (Low, Confirmed).

Area: HTTPS verification error text (exit 60).

Repro: sign test leaves from a test CA `ca.pem`, serve them over HTTPS, and run each with `curl -sS https://localhost:<port>/ --cacert ca.pem --ssl-no-revoke`:
- 43101: leaf signed by a different key that has the same issuer DN
- 43102: leaf signed by a CA:FALSE leaf
- 43103: leaf with only the clientAuth EKU
- 43110: leaf under a name-constrained intermediate (permitted `allowed.test`)
- 43111: chain that violates pathlen:0
- 43113: leaf with a corrupted signature

Expected (REF): exit 60 in every case, with:
- 43101 and 43113: `curl: (60) schannel: error 0x00000008`
- 43102 and 43111: `curl: (60) schannel: error 0x00000400`
- 43103: `curl: (60) schannel: error 0x00000010`
- 43110: `curl: (60) schannel: error 0x00004000`

Actual (PORT): exit 60 in every case, always `curl: (60) schannel: the certificate or certificate chain is based on an untrusted root`. On the Linux build the clientAuth case says `self-signed certificate in certificate chain (19)` where REF2 says `unsuitable certificate purpose (26)` (that serverAuth-EKU refusal itself is BL-5007; this task is the Schannel text mapping).

Where to start: `Curl.Networking.UnitLibrary/TlsFailureMessages.cs`, the mapping from Schannel chain status (`SchannelPeerFailedVerification`, constants near lines 25-47) to message. Any unmapped `X509ChainStatusFlags` value should print `schannel: error 0x%08x` with the CERT_TRUST_* value (8 hex digits, lowercase `0x` prefix). Map .NET flags to CERT_TRUST_* values: 0x8 = SignatureNotValid/IS_NOT_SIGNATURE_VALID, 0x400 = InvalidBasicConstraints, 0x10 = NotValidForUsage, 0x4000 = HasNotPermittedNameConstraint.

## Acceptance criteria

- [x] Unit tests in `Curl.Networking.UnitTests/TlsFailureMessagesTests.cs` assert, for the Schannel profile with `--cacert`, that chain status `NotSignatureValid` yields `schannel: error 0x00000008`, `InvalidBasicConstraints` yields `schannel: error 0x00000400`, `NotValidForUsage` yields `schannel: error 0x00000010`, and `HasNotPermittedNameConstraint` yields `schannel: error 0x00004000`, each with exit 60.
- [x] A test shows an unmapped flag still prints `schannel: error 0x%08x` with its CERT_TRUST value.
- [x] Regression: an untrusted-root chain still prints `schannel: the certificate or certificate chain is based on an untrusted root`, and the existing expired, revoked and incomplete-chain messages are unchanged.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Mock servers in the review were Python; build stand-ins in PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md). Measure real curl (the platform build) before pinning any output text. Schannel profile only; do not change the OpenSSL profile's text here. The flag-to-value table above is my reading of the review's expected values; confirm each against the CERT_TRUST_* definitions and real curl.

## Log

- 2026-11-12: Created.
- 2026-11-12: Backlog -> Doing. Criteria at claim: c37c30b3 591cabf5 80a3b137 a0b49a29.
- 2026-11-12: Chain statuses other than expired/revoked/incomplete/untrusted-root print `schannel: error 0x<CERT_TRUST_* value>` (.NET flag values equal CERT_TRUST_*). Fast tests (Networking) green, Measure-CodeQuality clean for Curl.Networking.UnitLibrary.
