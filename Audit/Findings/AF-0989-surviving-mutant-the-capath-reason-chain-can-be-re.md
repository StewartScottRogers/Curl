---
id: AF-0989
title: Surviving mutant: the --capath reason chain can be rebuilt without requiring the server-auth purpose (true -> false)
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-25_1306.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs:ReasonChainFromAnchors-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs:207:true
task: BL-5791
tasks: BL-5791
found: 2026-11-22
found-at: 94c9744bc919995aff928a0378582601ddbbe990
scorecard: 2026-11-22_1817.md
duplicate-of:
closed: 2026-11-25
closed-how: mechanical
closed-by: 2026-11-25_1306.md
---
# AF-0989 - Surviving mutant: the --capath reason chain can be rebuilt without requiring the server-auth purpose (true -> false)

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs:207`: Surviving mutant: the --capath reason chain can be rebuilt without requiring the server-auth purpose (true -> false).

## Evidence

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs:207`

Seeded mutation run (with the 3 failing ECH tests left out): 'RebuildAgainstAnchors(chain, anchors, requireServerAuthentication: true, out _)' -> 'requireServerAuthentication: false' survived. ReasonChainFromAnchors picks the chain whose status gives the OpenSSL build's verify reason. Without the server-auth requirement, a leaf with the wrong extended key usage under a --capath root reports a reason from a chain that ignores the purpose, so the error text changes. No test covers a wrong-purpose leaf on this path.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ServerCertificateVerification.CaCertificates.cs:207:true -Member ReasonChainFromAnchors -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-25 | 2026-11-25_1306.md | reproduces: no | Ran the -Site command: site resolved within ReasonChainFromAnchors to line 233, 'requireServerAuthentication: true' -> 'false', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-22: filed proposed.
- 2026-11-25: accepted -> closed. Re-audit 2026-11-25_1306.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
