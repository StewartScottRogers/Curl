---
id: AF-0719
title: Surviving mutant: --capath can take a file named 'xxxxxxxx.' (no digit) as a hashed certificate
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:IsHashedCertificateFileName-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:698:>
task: none
tasks:
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0719 - Surviving mutant: --capath can take a file named 'xxxxxxxx.' (no digit) as a hashed certificate

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:698`: Surviving mutant: --capath can take a file named 'xxxxxxxx.' (no digit) as a hashed certificate. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:698`

Mutant 'name is { Length: > 9 }' -> '>= 9' survived. A 9-character name such as 'abcdef01.' passes every other check (the dot at index 8, hex before it, and an empty suffix contains no non-digit), so the mutant counts it among the CA directory's subject hashes, which changes trust decisions. The boundary is untested.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:698:> -Member IsHashedCertificateFileName -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-12: filed proposed.
