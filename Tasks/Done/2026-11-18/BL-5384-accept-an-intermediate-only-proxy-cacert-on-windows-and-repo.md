---
id: BL-5384
title: Accept an intermediate-only --proxy-cacert on Windows and report a revoked proxy certificate as revoked
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.ChainErrors.cs, Curl.Networking.UnitLibrary/ServerCertificateVerification.AnchorRevocation.cs, Curl.Networking.UnitLibrary/RevocationListDownload.cs, Curl.Networking.UnitTests/IntermediateOnlyProxyCacertTests.cs]
requirement: none
created: 2026-11-17
completed: 2026-11-17
---
# BL-5384 — Accept an intermediate-only --proxy-cacert on Windows and report a revoked proxy certificate as revoked

## Goal

On Windows, `--proxy-cacert int1.pem` holding only the issuing intermediate lets an HTTPS proxy whose leaf chains to it through, and a revoked proxy leaf is reported with the "has been revoked" text instead of "the certificate chain is incomplete".

## Context

From the outside fitness review of 2026-11-17 (commit 9561bbe, `preview` build), finding STD-095 (Medium, Confirmed twice: fails closed; a proxy CA bundle holding only the issuing intermediate cannot be used with any option). Area: HTTPS proxy trust, Windows. Same root cause as STD-055 (BL-5385) and STD-092 (BL-5375): the Windows chain engine reports `RevocationStatusUnknown` on every element of a partial chain that ends at a non-self-signed custom anchor. Keep the shared code in `ServerCertificateVerification.cs` consistent across the three tasks.

**Repro.**
1. Root `root.pem` -> intermediate `int1.pem` (CA:TRUE, keyCertSign/cRLSign, CRL distribution point to a reachable, valid, empty CRL) -> leaf `leaf1.pem` (SAN localhost, CRL DP to a reachable valid `int1.crl`).
2. An HTTPS forward proxy on 127.0.0.1:45090 presenting `leaf1`+`int1` that answers absolute-form GETs with 200 and a body. Any HTTP origin on 127.0.0.1:45081.
3. `curl -sS -m 15 -x https://localhost:45090 --proxy-cacert int1.pem --ssl-no-revoke http://localhost:45081/`
4. Repeat with the proxy presenting a leaf that is listed as revoked in `int1.crl`.

**Expected (REF).** Step 3 exits 0 with the proxied body. REF does not apply `--ssl-no-revoke` to the proxy and still checks the proxy's revocation: step 4 gives `curl: (60) schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked`.
**Actual (PORT).** Step 3 gives `curl: (60) schannel: the certificate chain is incomplete`, exit 60. Step 4 gives the same "chain is incomplete" text instead of the revoked text.
**Where to start.** `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`, the proxy policy instance, `CaCertificateFileAnchorsChain` and `HasOnlyMissingRootOrAnchorRevocationStatus` (both exist in the file now).

## Acceptance criteria

- [x] New tests in `Curl.Networking.UnitTests` (a new file, e.g. `ServerCertificateVerificationTests.ProxyIntermediateAnchor.cs`) prove a proxy policy accepts a chain whose only anchor is a non-self-signed `--proxy-cacert` intermediate, with and without `--ssl-no-revoke`.
- [x] A test proves a proxy leaf revoked by that intermediate's CRL is refused with the "has been revoked" message, not the "chain is incomplete" message.
- [x] A test proves `--proxy-cacert` with an unrelated root is still refused (exit 60).
- [x] At least one test builds a real chain with `X509Chain` (generated certificates and a real CRL) rather than stubbing chain statuses, because BL-5317's stubbed tests hid STD-055 (see BL-5385).
- [x] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [x] Hand check recorded in the Log against a live proxy handshake: step 3 exits 0 with the body; step 4 exits 60 with the "has been revoked" text; `--proxy-cacert` with an unrelated root still exits 60. Measure real curl with `Record-CurlExchange.ps1` where possible.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Decided by Claude: `touches` widened to ChainErrors.cs (where the anchor logic lives), a new AnchorRevocation.cs and RevocationListDownload.cs; no other task in Doing touches Curl.Networking. The proxy options never carry `--ssl-no-revoke` (TlsClientOptionsMapping.ProxyOf), so no Engine change was needed. Platform notes: the real-chain tests are `OSCondition(Windows)`; the stubbed ones are platform-neutral, so CI on Linux and macOS stays green.

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: step 3 exits 0 with the body; step 4 exits 60 with the "has been revoked" text; `--proxy-cacert` with an unrelated root still exits 60.
- Related: STD-055 (BL-5385), STD-092 (BL-5375, name constraints; its acceptance check must still pass).

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 905311f5 84865322 d9eec71c a8d272ff 7ae10ef7 1dd8863f a0b49a29.
- 2026-11-17: Done. Root cause measured: with real certificates and a valid CRL served on loopback, the Windows engine marks every element of the partial chain `RevocationStatusUnknown`+`OfflineRevocation`, revoked leaf or not, so nothing in the chain statuses could tell them apart. `ServerCertificateVerification` now reads the CRL itself (ADR-1294): `ServerCertificateVerification.AnchorRevocation.cs` (CDP parsing, list validation, `FetchRevocationList` seam), `RevocationListDownload.cs` (thin HttpClient fetch, ADR-0083 exclusion), and `ChainErrors.cs` (accept when clean, "has been revoked" when a usable CRL lists a certificate). Tests: `IntermediateOnlyProxyCacertTests.cs` (real `X509Chain` tests with generated certs and CRL on Windows; stubbed-status tests, run everywhere, drive each CRL branch). Measure-CodeQuality for Curl.Networking.UnitLibrary: 100% line, 100% branch, 0 failing.
- Hand check (live proxy handshake, throwaway C# proxy presenting leaf1+int1, CRL served on 127.0.0.1, `Curl.Console` build): valid leaf, `--proxy-cacert int1.pem` with and without `--ssl-no-revoke`: exit 0, body `proxied`. Revoked leaf: exit 60 `schannel: trust for this certificate or one of the certificates in the certificate chain has been revoked` (also with `--ssl-no-revoke`, which does not reach the proxy, as in REF). Unrelated root as `--proxy-cacert`: exit 60 `the certificate chain is incomplete`. Real curl was not available to re-measure; the REF texts are those in the task.
- 2026-11-17: Doing -> Done. CRL read for certificates below an intermediate-only anchor; revoked proxy leaf reports revoked
