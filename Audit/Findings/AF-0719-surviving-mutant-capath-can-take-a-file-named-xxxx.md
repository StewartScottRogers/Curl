---
id: AF-0719
title: Surviving mutant: --capath can take a file named 'xxxxxxxx.' (no digit) as a hashed certificate
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:IsHashedCertificateFileName-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:698:>
task: BL-5078
tasks: BL-5078
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

- 2026-11-13 | 2026-11-13_2321.md | not re-audited | Ran the -Site command: outcome site-missing. IsHashedCertificateFileName now reads name is { Length: >= 10 } with a comment that a bare hash-dot name never names a file, so no > site remains in the member: not re-audited.
- 2026-11-14 | 2026-11-14_1335.md | not re-audited | Ran the -Site command: outcome site-missing (resolvedLine null). IsHashedCertificateFileName moved to line 724 and now reads 'name is { Length: >= 10 }'. For information, the same operator at the new site (ServerCertificateVerification.cs:724:>=) was killed. Recorded as not re-audited.
- 2026-11-14 | 2026-11-14_1801.md | not re-audited | Ran the -Site command: outcome site-missing, resolvedLine null, no mutant run. IsHashedCertificateFileName now reads 'name is { Length: >= 10 } && name[8] == '.' && ...' (ServerCertificateVerification.cs:724-728), so the member holds no ' > ' site. Not re-audited.
- 2026-11-15 | 2026-11-15_0222.md | not re-audited | Ran the -Site command: outcome site-missing, resolvedLine null. IsHashedCertificateFileName is now 'name is { Length: >= 10 } && name[8] == '.' && ...' with no ' > ' operator in the member, so the mutant cannot be applied.
- 2026-11-15 | 2026-11-15_1159.md | not re-audited | Ran the -Site reproduction: outcome site-missing. IsHashedCertificateFileName is now 'name is { Length: >= 10 } && name[8] == '.' && ...' (ServerCertificateVerification.cs:724-728), with no ' > ' site in the member, so the mutant could not be rerun.

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
