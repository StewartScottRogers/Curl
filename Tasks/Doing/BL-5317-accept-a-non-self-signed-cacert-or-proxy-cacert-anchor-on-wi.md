---
id: BL-5317
title: Accept a non-self-signed --cacert or --proxy-cacert anchor on Windows with revocation checking on
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NonSelfSignedAnchorRevocation.cs]
requirement: none
created: 2026-11-16
completed:
---
# BL-5317 — Accept a non-self-signed --cacert or --proxy-cacert anchor on Windows with revocation checking on

## Goal

On Windows, `--cacert` and `--proxy-cacert` files that hold a non-self-signed certificate (an intermediate CA) are accepted as a trust anchor with revocation checking on (the default, `--no-sessionid`, `--ssl-revoke-best-effort`, and the HTTPS proxy), as they already are under `--ssl-no-revoke`; an unrelated CA is still rejected and a revoked certificate still fails.

## Context

From the outside fitness review of 2026-11-16 (commit 64e9144, `preview` build), finding STD-055 (Medium, Confirmed: each route twice). Area: HTTPS trust anchors, Windows build. **Re-reported:** the earlier fix BL-5100 repaired only the `--ssl-no-revoke` route, so it is incomplete. It fails closed, but CA bundles that hold only the issuing intermediate break in the default configuration.

**Repro.**
1. Make a root `ca.pem`; an intermediate `int.pem` (CA:TRUE, keyUsage `keyCertSign,cRLSign`) signed by the root; a localhost leaf `chained.pem` signed by `int`; a localhost leaf `good.pem` signed by the root. Give every certificate a CRL distribution point and serve valid empty CRLs (for example `http://127.0.0.1:32080/ca.crl` and `/int.crl`).
2. Serve `chained.pem + int.pem` on 127.0.0.1:32112, `good.pem` on 32100, an HTTPS forward proxy with `good.pem` on 32092, and a plain HTTP origin on 32081. (PowerShell or a C# file-based app, never Python.)
3. Run:
   - `curl -sS https://localhost:32112/ --cacert int.pem`
   - `curl -sS https://localhost:32100/ --cacert good.pem`
   - `curl -sS https://localhost:32100/ --cacert good.pem --no-sessionid`
   - `curl -sS https://localhost:32112/ --cacert int.pem --ssl-revoke-best-effort`
   - `curl -sS http://localhost:32081/ -x https://localhost:32092 --proxy-cacert good.pem`

**Expected (reference curl, Schannel build).** Every command exits 0 with the body (the reference's exclusive-root engine treats any certificate in the CA file as an anchor). With `--ssl-no-revoke` the port now already exits 0 for the first command.

**Actual (port).** Each prints `curl: (60) schannel: the certificate chain is incomplete` and `More details here: https://curl.se/docs/sslcerts.html`, exit 60. Controls match the reference: `--cacert ca2.pem` from an unrelated root exits 60 in both.

**Where to start.** `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`: `CaCertificateFileAnchorsChain` (line 515) calls `HasOnlyMissingRootStatus` (line 526), which allows only `PartialChain | UntrustedRoot`; with revocation on, a chain ending at a non-self-signed anchor also carries `RevocationStatusUnknown` / `OfflineRevocation`. Ignore revocation-unknown on the anchor element itself (or rebuild with the listed certificate as a custom root), and apply the same to the proxy policy. Existing tests for the BL-5100 fix are in `Curl.Networking.UnitTests/ServerCertificateVerificationTests.NonSelfSignedAnchor.cs`; extend that pattern in a new file.

## Acceptance criteria

- [x] New tests in `Curl.Networking.UnitTests/ServerCertificateVerificationTests.NonSelfSignedAnchorRevocation.cs` (certificates built in the test with `CertificateRequest`, chain-status stand-ins, no network) prove that a chain ending at a non-self-signed `--cacert` anchor is accepted when its only extra statuses are `RevocationStatusUnknown` / `OfflineRevocation` on the anchor element, for the default revocation mode, with `--no-sessionid`, and with `--ssl-revoke-best-effort`.
- [x] A test proves the same acceptance for the HTTPS-proxy policy (`--proxy-cacert`).
- [x] A test proves a chain to an unrelated CA is still rejected (exit 60 path), and a leaf or intermediate that is revoked (`Revoked` status) is still rejected with `trust for this certificate or one of the certificates in the certificate chain has been revoked`; a revocation-unknown status on the leaf itself is not newly tolerated.
- [x] Tests are platform-neutral, or marked `[OSCondition(OperatingSystems.Windows)]` where the Windows chain engine is the point.
- [ ] The Log records the review's hand check on Windows: all five commands exit 0 with the body; `--cacert ca2.pem` still exits 60; a leaf revoked in `int.crl` still exits 60 with the revoked text.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Related, already Done: BL-5100 (the `--ssl-no-revoke` route). Measure real curl (Schannel) with `Record-CurlExchange.ps1` where possible. Mock servers must be PowerShell or a C# file-based app, never Python (CLAUDE.md).

- Fix: `ServerCertificateVerification.CaCertificateFileAnchorsChain` also accepts RevocationStatusUnknown/OfflineRevocation when the chain's last element (the anchor) is not self-signed and no element below it carries them (new seam `ReadCertificatesBelowAnchorStatus`). Revoked, unrelated anchors and leaf-level unknown are still refused. The proxy policy uses the same class, so one fix covers both; the test for it is named after it.
- Not done: the live Windows hand check against real curl with CRL servers (five commands) was not run in this unattended lane; unit tests stand in. Quality run for Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-11-16: Created.
- 2026-11-16: Backlog -> Doing. Criteria at claim: 39adc37a aa88da6a 2f2b4f26 6c17436b 3a7b118d a0b49a29.
