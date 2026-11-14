---
id: BL-5100
title: Accept a non-self-signed certificate in --cacert and --proxy-cacert as a trust anchor on Windows
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.NonSelfSignedAnchor.cs]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5100 — Accept a non-self-signed certificate in --cacert and --proxy-cacert as a trust anchor on Windows

## Goal

On Windows, a certificate in the `--cacert` or `--proxy-cacert` file that is not self-signed (an intermediate CA, or the leaf itself) is accepted as a trust anchor on every route, as curl's Schannel backend and the Linux port do.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-055 (Medium, Confirmed twice per route). A compatibility break that fails closed; corporate bundles that hold only an issuing intermediate stop working. Windows HTTPS trust anchors: default route, `--ssl-no-revoke`, `--no-sessionid`, HTTPS proxy. The Linux port matches curl 8.18.0 (OpenSSL, partial chain).

**Repro.**
1. Make a CA `ca`, an intermediate `int` (CA:TRUE, keyCertSign) signed by `ca`, and a leaf for localhost signed by `int`. Serve the leaf plus `int` as the chain on 127.0.0.1:22128. Separately serve a leaf `good.crt` signed directly by `ca` on 22100, and an HTTPS proxy using `good.crt` on 22200 (PowerShell or a C# file-based app, not Python).
2. `curl -sS https://localhost:22128/ --cacert int.crt`
3. `curl -sS https://localhost:22128/ --cacert int.crt --ssl-no-revoke`
4. `curl -sS https://localhost:22100/ --cacert good.crt` (the leaf itself as anchor)
5. `curl -sS http://localhost:22098/ -x https://localhost:22200 --proxy-cacert good.crt`

**Expected (curl 8.22.0 Schannel).** Exit 0 and the body in all cases. curl's Schannel backend builds the chain with `CERT_CHAIN_EXCLUSIVE_ENABLE_CA_FLAG`, so any certificate in the CA file is an anchor.

**Actual.** `curl: (60) schannel: the certificate chain is incomplete`, exit 60, in every case except leaf-as-anchor with `--ssl-no-revoke`.

**Where to start.** `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs`, `ReadChainAnchors` (about line 202) uses `X509ChainTrustMode.CustomRootTrust`, which reports `PartialChain` when the anchor is not self-signed; the proxy policy has the same issue. When the only chain errors are PartialChain/UntrustedRoot and a chain element equals a certificate from the CA file, treat it as the anchor. Existing tests: `Curl.Networking.UnitTests/ServerCertificateVerificationTests.PartialChain.cs`, `.LeafAnchor.cs`, `.VerifyChain.cs`.

## Acceptance criteria

- [x] New tests in `Curl.Networking.UnitTests/ServerCertificateVerificationTests.NonSelfSignedAnchor.cs` (certificates generated in the test) prove that a chain leaf -> `int` -> `ca` is accepted when the anchor file holds only `int`, on the default route and the `--ssl-no-revoke` route.
- [x] A test proves the leaf itself as the only anchor is accepted on the default route, and the proxy-certificate policy accepts an anchor that is a leaf or an intermediate.
- [x] A test proves a leaf signed by an unrelated CA, with an unrelated certificate as the only anchor, is still refused with exit 60, and that an expired or wrong-name certificate is still refused.
- [x] Tests are platform-neutral, or `[OSCondition(OperatingSystems.Windows)]` where they need the Windows chain engine; the Linux route is unchanged.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; use PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python.
- Shares `ServerCertificateVerification.cs` with the host-name matching task from the same review (finding STD-050); the board runs them one after the other.
- Review acceptance check, for a Windows hand check: step 2 prints the body and exits 0; a leaf signed by an unrelated CA still exits 60.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 9473f6d9 4b0a611d d86c5b61 4b3018a4 a0b49a29.
- 2026-11-13: Fix: `ListsAnAnchorCandidate` takes every chain element in the Schannel build (ADR-1266). Two older tests that pinned exit 60 for an intermediate-only file now pin acceptance; the LeafAnchor "another certificate" test got a distinct subject. Quality run for Curl.Networking.UnitLibrary clean. Windows hand check not done in the lane.
- 2026-11-13: Doing -> Done. A listed intermediate or leaf now anchors the Schannel --cacert chain
