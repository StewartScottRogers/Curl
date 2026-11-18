---
id: BL-5384
title: Accept an intermediate-only --proxy-cacert on Windows and report a revoked proxy certificate as revoked
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs, Curl.Networking.UnitTests/IntermediateOnlyProxyCacertTests.cs]
requirement: none
created: 2026-11-17
completed:
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

- [ ] New tests in `Curl.Networking.UnitTests` (a new file, e.g. `ServerCertificateVerificationTests.ProxyIntermediateAnchor.cs`) prove a proxy policy accepts a chain whose only anchor is a non-self-signed `--proxy-cacert` intermediate, with and without `--ssl-no-revoke`.
- [ ] A test proves a proxy leaf revoked by that intermediate's CRL is refused with the "has been revoked" message, not the "chain is incomplete" message.
- [ ] A test proves `--proxy-cacert` with an unrelated root is still refused (exit 60).
- [ ] At least one test builds a real chain with `X509Chain` (generated certificates and a real CRL) rather than stubbing chain statuses, because BL-5317's stubbed tests hid STD-055 (see BL-5385).
- [ ] Tests are platform-neutral (or OS-conditioned where curl differs by platform).
- [ ] Hand check recorded in the Log against a live proxy handshake: step 3 exits 0 with the body; step 4 exits 60 with the "has been revoked" text; `--proxy-cacert` with an unrelated root still exits 60. Measure real curl with `Record-CurlExchange.ps1` where possible.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check: step 3 exits 0 with the body; step 4 exits 60 with the "has been revoked" text; `--proxy-cacert` with an unrelated root still exits 60.
- Related: STD-055 (BL-5385), STD-092 (BL-5375, name constraints; its acceptance check must still pass).

## Log

- 2026-11-17: Created.
- 2026-11-17: Backlog -> Doing. Criteria at claim: 905311f5 84865322 d9eec71c a8d272ff 7ae10ef7 1dd8863f a0b49a29.
