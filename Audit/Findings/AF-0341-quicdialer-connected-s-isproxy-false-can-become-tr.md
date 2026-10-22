---
id: AF-0341
title: QuicDialer.Connected's isProxy: false can become true with no test failing, so an HTTP/3 transfer reports its verify result as the proxy's
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/QuicDialer.cs:Connected-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/QuicDialer.cs:432:false
task: none
tasks:
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0341 - QuicDialer.Connected's isProxy: false can become true with no test failing, so an HTTP/3 transfer reports its verify result as the proxy's

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/QuicDialer.cs:432`: QuicDialer.Connected's isProxy: false can become true with no test failing, so an HTTP/3 transfer reports its verify result as the proxy's.

## Evidence

Location: `Curl.Networking.UnitLibrary/QuicDialer.cs:432`

Sampled mutant (seed 0) survived: 'verifier.Observed.ReportVerifyResult(events, isProxy: false, ...)' became 'isProxy: true'. ReportCertificateVerifyResult's isProxy decides whether the code is recorded as the origin's SSL verify result or the proxy's, which is what %{ssl_verify_result} / %{proxy_ssl_verify_result} and CURLINFO_SSL_VERIFYRESULT / CURLINFO_PROXY_SSL_VERIFYRESULT read. A direct HTTP/3 transfer would therefore write the wrong -w bytes. No Curl.Networking.UnitTests test checks the flag on the QUIC path.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/QuicDialer.cs:432:false -Member Connected -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
