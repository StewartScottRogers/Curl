---
id: AF-0954
title: Surviving mutant: a chain can be refused as relying on an unsent intermediate when no peer certificates were captured
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:RelyingOnAnIntermediateTheServerDidNotSend-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:343:>
task: BL-5723
tasks: BL-5723
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-21: filed proposed.
