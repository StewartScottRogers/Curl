---
id: BL-5385
title: Accept an intermediate-only --cacert with revocation on against a live Windows handshake
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/IntermediateOnlyCacertRevocationTests.cs]
requirement: none
created: 2026-11-17
completed:
---
# BL-5385 — Accept an intermediate-only --cacert with revocation on against a live Windows handshake

## Goal

On Windows, `--cacert int.pem` (and `--proxy-cacert`, and `--ssl-revoke-best-effort`) holding only the issuing intermediate lets a live TLS server through with revocation checking on, and a revoked leaf is reported with the "has been revoked" text.

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-055 (Medium, Confirmed by two reviewers, 3+ runs). STILL OPEN after BL-5317: its unit tests passed only because the seam stubbed the chain statuses, so the fix never worked on a live handshake. Area: HTTPS server and proxy certificate verification, Windows (fails closed; CA bundles holding only the issuing intermediate break in the default configuration).

Shared root cause with STD-095 (BL-5384) and STD-092 (BL-5375) in `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`. Fixing this one as planned would expose STD-092 (name constraints ignored on a non-self-signed anchor) in the default configuration, so the STD-092 check must be applied on the same path.

**Repro.**
1. Root `ca.pem` -> intermediate `int.pem` (CRL DP `http://127.0.0.1:41080/ca.crl`) -> leaf `chained.pem` (SAN localhost, CRL DP `http://127.0.0.1:41080/int.crl`). Serve valid CRLs at both URLs with any HTTP server on 41080.
2. Serve `chained.pem`+`int.pem` over TLS on 127.0.0.1:41112, answering 200 `hello`.
3. `curl -sS -m 15 https://localhost:41112/ --cacert int.pem`
4. Also `--ssl-revoke-best-effort`, and `-x https://localhost:<proxy> --proxy-cacert int.pem http://...`.

**Expected (REF).** Prints `hello`, exit 0. With a revoked leaf, REF prints `trust for this certificate or one of the certificates in the certificate chain has been revoked`.
**Actual (PORT).** `curl: (60) schannel: the certificate chain is incomplete`, exit 60, for every variant (a revoked leaf gets the same text). A leaf certificate used as its own anchor now works. A .NET check shows a `CustomRootTrust` chain with the intermediate as custom root and online revocation yields `PartialChain, RevocationStatusUnknown, OfflineRevocation` on every element, the leaf included, even when the CRL was fetched and is valid. So BL-5317's condition "no element below the anchor carries RevocationStatusUnknown" is never true on Windows.
**Where to start.** `HasOnlyMissingRootOrAnchorRevocationStatus` in `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs` (commit 0fa12432). Check the leaf's revocation yourself against the CRL issued by the anchor, or rebuild the chain treating the anchor as a trusted root. Apply the STD-092 name-constraint check on the same path.

## Acceptance criteria

- [ ] New tests in `Curl.Networking.UnitTests` (a new file, e.g. `ServerCertificateVerificationTests.LiveIntermediateAnchorRevocation.cs`) build a real chain (generated root, intermediate, leaf and a real CRL served from a loopback listener) and run the production verification code on it with no stubbed chain statuses; they prove the chain is accepted with revocation on.
- [ ] A test with the same real chain, leaf listed as revoked in the intermediate's CRL, proves refusal with the "has been revoked" text, not "the certificate chain is incomplete".
- [ ] A test proves `--ssl-revoke-best-effort` and the proxy policy (`--proxy-cacert`) take the same path and accept the same chain.
- [ ] A test proves a leaf with a name-constraint violation under the anchor is still refused (STD-092's acceptance check keeps passing; coordinate with BL-5375).
- [ ] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [ ] Hand check recorded in the Log against a live TLS server (not a stub): step 3 prints `hello`, exit 0; with a revoked leaf it exits 60 with the "has been revoked" text. Measure real curl with `Record-CurlExchange.ps1` where possible.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: step 3 prints `hello`, exit 0, against a live server (not a stub). With a revoked leaf it exits 60 with the "has been revoked" text. STD-092's acceptance check still passes.
- Related: STD-095 (BL-5384), STD-092 (BL-5375).

## Log

- 2026-11-17: Created.
