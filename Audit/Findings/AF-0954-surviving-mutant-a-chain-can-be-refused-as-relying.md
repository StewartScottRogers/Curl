---
id: AF-0954
title: Surviving mutant: a chain can be refused as relying on an unsent intermediate when no peer certificates were captured
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-22_0210.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:RelyingOnAnIntermediateTheServerDidNotSend-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:343:>
task: BL-5723
tasks: BL-5723
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed: 2026-11-22
closed-how: mechanical
closed-by: 2026-11-22_0210.md
---
# AF-0954 - Surviving mutant: a chain can be refused as relying on an unsent intermediate when no peer certificates were captured

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:343`: Surviving mutant: a chain can be refused as relying on an unsent intermediate when no peer certificates were captured. Reported by an auditor flagged unreliable in 2026-11-21_1740.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:343`

Line 343: && peerCertificates.Length > 0 mutated to >= 0 survived. With an empty peerCertificates array the mutant goes on to Any(element => !WasSent(...)), finds every middle chain element unsent and Judge (line 305) refuses the handshake with exit 60 'unable to get local issuer certificate (20)'. The guard decides whether the OpenSSL-build refusal runs at all, and no test passes an empty peer list.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:343:> -Member RelyingOnAnIntermediateTheServerDidNotSend -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-22 | 2026-11-22_0210.md | reproduces: no | Ran the -Site command: resolvedLine 343, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-21: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_0210.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
