---
id: AF-0849
title: Surviving mutant: a resumed session that allows no early data can still be used for 0-RTT
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-18_0810.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:EarlyDataProtocolOf-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:372:>
task: BL-5369
tasks: BL-5369
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed: 2026-11-18
closed-how: mechanical
closed-by: 2026-11-18_0810.md
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

- 2026-11-18 | 2026-11-18_0810.md | reproduces: no | Ran the -Site command: HandBuiltTlsProvider.cs:372:> [EarlyDataProtocolOf] resolved within the member to line 405 'MaxEarlyDataSize: > 0' -> '>= 0', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-17: filed proposed.
- 2026-11-18: accepted -> closed. Re-audit 2026-11-18_0810.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
