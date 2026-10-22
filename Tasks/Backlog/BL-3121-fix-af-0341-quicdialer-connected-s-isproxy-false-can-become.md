---
id: BL-3121
title: Fix AF-0341: QuicDialer.Connected's isProxy: false can become true with no test failing, so an HTTP/3 transfer reports its verify result as the proxy's
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-22
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-22: Created.
