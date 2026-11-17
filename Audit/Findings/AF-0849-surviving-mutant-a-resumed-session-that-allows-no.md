---
id: AF-0849
title: Surviving mutant: a resumed session that allows no early data can still be used for 0-RTT
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:EarlyDataProtocolOf-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:372:>
task: none
tasks:
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0849 - Surviving mutant: a resumed session that allows no early data can still be used for 0-RTT

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:372`: Surviving mutant: a resumed session that allows no early data can still be used for 0-RTT.

## Evidence

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:372`

session is { MaxEarlyDataSize: > 0 } -> >= 0 survived. A session ticket whose max_early_data_size is 0 (the server forbids early data) would give an early-data protocol, so --tls-earlydata would send the request as 0-RTT data the server did not allow (request bytes and the handshake outcome). No test resumes a session with MaxEarlyDataSize 0.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:372:> -Member EarlyDataProtocolOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-17: filed proposed.
