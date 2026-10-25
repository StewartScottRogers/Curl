---
id: AF-0436
title: ObservePeerVerification's ChainElements.Count > 0 can become >= 0 with no test failing
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:ObservePeerVerification-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:426:>
task: none
tasks:
found: 2026-10-25
found-at: 03fe150efe7a6f73cd1a1c8fbd2c7bda2248009b
scorecard: 2026-10-25_0941.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0436 - ObservePeerVerification's ChainElements.Count > 0 can become >= 0 with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:426`: ObservePeerVerification's ChainElements.Count > 0 can become >= 0 with no test failing. Reported by an auditor flagged unreliable in 2026-10-25_0941.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:426`

Sampled mutant (seed 0) survived: 'var reportedChain = chain is { ChainElements.Count: > 0 }' -> '>= 0'. With the mutant, a chain that was built with no elements and no chain errors is reported as ListChainElements(chain), an empty list, in place of the certificates the server sent (peerCertificates). That list becomes PeerVerification's reported chain, which ConnectResult.PeerCertificates and the -v certificate lines are built from. No Curl.Networking.UnitTests test gives the verifier an empty chain, so a regression that drops the peer's certificates goes unnoticed.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:426:> -Member ObservePeerVerification -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-25: filed proposed.
