---
id: BL-3121
title: Fix AF-0341: QuicDialer.Connected's isProxy: false can become true with no test failing, so an HTTP/3 transfer reports its verify result as the proxy's
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3121 — Fix AF-0341: QuicDialer.Connected's isProxy: false can become true with no test failing, so an HTTP/3 transfer reports its verify result as the proxy's

## Goal

The defect the audit office reported as AF-0341 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0341 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0341-quicdialer-connected-s-isproxy-false-can-become-tr.md`.

Location: `Curl.Networking.UnitLibrary/QuicDialer.cs:432`

Location: `Curl.Networking.UnitLibrary/QuicDialer.cs:432`

Sampled mutant (seed 0) survived: 'verifier.Observed.ReportVerifyResult(events, isProxy: false, ...)' became 'isProxy: true'. ReportCertificateVerifyResult's isProxy decides whether the code is recorded as the origin's SSL verify result or the proxy's, which is what %{ssl_verify_result} / %{proxy_ssl_verify_result} and CURLINFO_SSL_VERIFYRESULT / CURLINFO_PROXY_SSL_VERIFYRESULT read. A direct HTTP/3 transfer would therefore write the wrong -w bytes. No Curl.Networking.UnitTests test checks the flag on the QUIC path.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/QuicDialer.cs:432:false -Member Connected -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-22 (lane 2): The fix is a test only: a QUIC handshake test in Curl.Networking.UnitTests (beside TcpConnectorQuicTests.cs, using Fakes/QuicTestServer.cs) that asserts the verify result is reported with isProxy false (origin, not proxy). Added Curl.Networking.UnitTests to touches for that test; BL-3127 holds that whole project, so requeued until it is Done. No other prerequisite found.
- 2026-10-22 (lane 1): Added `TcpConnectorQuicTests.VerifyResult.cs`: a QUIC handshake on the OpenSSL build (`matchesSchannelBuild: false`; the Schannel build reports no verify result) asserts the single `ReportCertificateVerifyResult` has `IsProxy` false. A lane may not run `Audit/Tools/Invoke-MutationTest.ps1` (audit paths are guarded), so the mutant was applied by hand instead: with `isProxy: true` at QuicDialer.cs:432 the test fails (killed), with the original line it passes; the source was restored unchanged. Test only, no production change, so no Measure-CodeQuality run was needed. `dotnet build` clean; fast tests green (44 test projects passed, 0 failed). The test is platform-neutral (in-memory QUIC server, no paths or OS text). The finding itself closes only on the quality auditor's re-audit.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Backlog. Needs Curl.Networking.UnitTests for the killing test; BL-3127 holds that project
- 2026-10-22: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-22: Doing -> Done. A QUIC handshake test pins the origin verify result's isProxy false, killing AF-0341's mutant
