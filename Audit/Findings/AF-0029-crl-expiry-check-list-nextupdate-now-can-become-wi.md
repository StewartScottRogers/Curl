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
- 2026-10-08 | 2026-10-08_0748.md | not re-audited | Ran the -Site command: outcome site-missing, resolvedLine null. DateRefusal now reads 'return list.NextUpdate <= now ? ... : null;' at CertificateRevocationListFile.cs:117, so the '<' site is gone. As extra evidence, -Site ...CertificateRevocationListFile.cs:117:<= (back to '<') was killed. Under the method a site-missing outcome is reported as not re-audited.
- 2026-10-08 | 2026-10-08_2315.md | not re-audited | Ran the -Site command: outcome site-missing, resolvedLine null. DateRefusal now reads 'return list.NextUpdate <= now ? ...' at line 117 with a comment that OpenSSL counts a nextUpdate equal to the moment as past, and CertificateRevocationListFileTests has Refusal_AtTheListsExpiryMoment_HasExpired. As extra evidence the moved site Curl.Networking.UnitLibrary/CertificateRevocationListFile.cs:117:<= ('<=' -> '<') was run and killed. But the finding's own site is missing, so this is not re-audited.
- 2026-10-09 | 2026-10-09_0225.md | not re-audited | Ran Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/CertificateRevocationListFile.cs:116:< -Member DateRefusal -ExcludeBaselineFailures -TimeoutSeconds 600: outcome site-missing, resolvedLine null, so this re-audit is not counted. The line (now 117) reads 'return list.NextUpdate <= now ? ... : null;'. As extra evidence I ran -Site ...CertificateRevocationListFile.cs:117:<= -Member DateRefusal: that mutant was killed.
- 2026-10-09 | 2026-10-09_0647.md | not re-audited | Ran the -Site reproduction: outcome site-missing for Curl.Networking.UnitLibrary/CertificateRevocationListFile.cs:116:< (resolvedLine null). DateRefusal's line 117 now reads 'return list.NextUpdate <= now ? ...' with a comment citing OpenSSL's X509_cmp_time, so no '<' site remains in the member. Not re-audited.
- 2026-10-09 | 2026-10-09_1435.md | not re-audited | Ran the -Site command: outcome site-missing, resolvedLine null. CertificateRevocationListFile.cs:117 now reads 'return list.NextUpdate <= now ? ...', so DateRefusal has no '<' site left to mutate. Not re-audited.

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
