---
id: BL-3289
title: Trust a leaf certificate named by --cacert on the Schannel build (tls-wrong-ca)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/ServerCertificateVerification.cs, Curl.Networking.UnitTests/ServerCertificateVerificationTests.LeafAnchor.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3289 — Trust a leaf certificate named by --cacert on the Schannel build (tls-wrong-ca)

## Goal

`curl -sS --ssl-no-revoke --cacert server.pem https://host/` (the CA file holds only the server's own leaf certificate) succeeds as Schannel curl 8.22.0 does, instead of exiting 60 "schannel: the certificate chain is incomplete".

## Context

Found by BL-3235's CLI TLS differential run (case `tls-wrong-ca` in `Curl.Conformance.Differential.UnitLibrary/Cases/CliTls/CliTlsCases.json`, also in the tester's `report_tls.txt`): real exit 0 with `ok\n`, Curl exit 60. Schannel curl builds a custom-root chain store from the --cacert file and accepts a leaf listed in it as a trust anchor. Measure with Record-CurlExchange.ps1 against a self-signed or CA-issued leaf, then fix the --cacert chain judgement in Curl.Networking.UnitLibrary/ServerCertificateVerification.cs.

## Acceptance criteria

- [x] A unit test in Curl.Networking.UnitTests pins that a --cacert file holding only the server's leaf certificate verifies that server, as measured from real curl.
- [x] The build is clean and the fast tests pass.

## Notes

- 2026-10-23 (lane 3): Curl.Tls.UnitLibrary has no chain code (no `X509Chain`), and Curl.Tls.UnitTests cannot reach Curl.Networking, so the first criterion could not be met as filed. The `--cacert` chain is built by .NET with `X509ChainTrustMode.CustomRootTrust` in `ServerCertificateVerification.ReadChainAnchors` and judged in `Judge` / `WithoutChainErrorsCurlTolerates`. A CA-issued leaf in `CustomTrustStore` does not end the chain, so .NET reports `PartialChain` and `TlsFailureMessages` gives "the certificate chain is incomplete". Schannel curl builds an exclusive-root chain engine from the file, where a listed leaf is a trust anchor.
- Plan: first measure `tls-wrong-ca` with Record-CurlExchange.ps1, against a CA-issued leaf and against a self-signed one. Then, in the Schannel build with `--cacert`, drop `RemoteCertificateChainErrors` when the chain's leaf is byte-equal to a `--cacert` certificate and the chain's only faults are `PartialChain` or `UntrustedRoot`. Pin it in the new test file `ServerCertificateVerificationTests.LeafAnchor.cs`.
- Touches and the first criterion were corrected from Curl.Tls to Curl.Networking. The board only allows criteria to be edited in Backlog, so the task was moved there to edit them. It was requeued because BL-3282 (in Doing) holds the whole Curl.Networking.UnitTests project; the board will offer the task again once they no longer overlap.
- 2026-10-23 (lane 3, second claim): Measurement taken from BL-3235's differential run against real curl 8.22.0 Schannel (`tls-wrong-ca`: exit 0, `ok`, with a CA-issued leaf alone in `--cacert`); not re-recorded, since the case already holds real curl's answer. Fix: `ServerCertificateVerification.SchannelTrustsListedLeaf` drops the chain error in the Schannel build with `--cacert` when the chain's leaf is byte-equal to a certificate in the chain policy's custom trust store and the only faults are `PartialChain`/`UntrustedRoot` (ADR-0797). Tests in `ServerCertificateVerificationTests.LeafAnchor.cs` pin the status through `ReadChainStatus` so they hold on every platform, plus one Windows-only test with the real chain status. The OpenSSL build's wider `X509_V_FLAG_PARTIAL_CHAIN` rule is filed as BL-3335. Measure-CodeQuality for Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 88a772e1 c46ca32a.
- 2026-10-23: Doing -> Backlog. Fix lands in Curl.Networking.UnitLibrary/ServerCertificateVerification.cs with tests in Curl.Networking.UnitTests, which BL-3282 holds; criteria and touches corrected from Curl.Tls
- 2026-10-23: Backlog -> Doing. Criteria at claim: 760e5ee9 c46ca32a.
- 2026-10-23: Doing -> Done. A --cacert file holding only the server's leaf verifies that server on the Schannel build, as curl 8.22.0 does
