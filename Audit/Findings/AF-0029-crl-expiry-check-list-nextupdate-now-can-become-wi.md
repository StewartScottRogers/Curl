---
id: AF-0029
title: CRL expiry check `list.NextUpdate < now` can become `<=` with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/CertificateRevocationListFile.cs:DateRefusal-lt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/CertificateRevocationListFile.cs:116:<
task: BL-1371
tasks: BL-1371
found: 2026-10-03
found-at: d1db9881553d55cd92c0e74bb41561c3dea9ea84
scorecard: 2026-10-03_1233.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0029 - CRL expiry check `list.NextUpdate < now` can become `<=` with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/CertificateRevocationListFile.cs:116`: CRL expiry check `list.NextUpdate < now` can become `<=` with no test failing. Reported by an auditor flagged unreliable in 2026-10-03_1233.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/CertificateRevocationListFile.cs:116`

Invoke-MutationTest (seed 0) mutated `return list.NextUpdate < now ? OpenSslVerifyResult.CertificateRevocationListHasExpired : null;` to `<=`; outcome survived. The boundary decides whether a CRL is reported as expired, a refusal a user can see.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/CertificateRevocationListFile.cs:116:< -Member DateRefusal -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: Mutant at CertificateRevocationListFile.cs:116 is killed.
- Actual: survived  Curl.Networking.UnitLibrary/CertificateRevocationListFile.cs:116 <

## Re-audits

- 2026-10-03 | 2026-10-03_1459.md | reproduces: yes | Seed-0 mutation run: CertificateRevocationListFile.cs:116 `<` to `<=` survived.
- 2026-10-07 | 2026-10-07_0844.md | reproduces: no | CertificateRevocationListFile.cs:117 now reads 'list.NextUpdate <= now'. By hand, same method as AF-0006: the reverse mutant '<=' -> '<' is killed by Refusal_AtTheListsExpiryMoment_HasExpired, so the expiry boundary is now pinned.
- 2026-10-07 | 2026-10-07_1336.md | not re-audited | Ran the -Site reproduction: outcome site-missing, so no mutant ran. DateRefusal now reads 'return list.NextUpdate <= now ? ...' (CertificateRevocationListFile.cs:117, with a comment citing OpenSSL's X509_cmp_time), so the '<' site no longer exists. Not re-audited.

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
