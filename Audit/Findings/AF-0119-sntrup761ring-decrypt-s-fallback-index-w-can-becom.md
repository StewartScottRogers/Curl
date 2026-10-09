---
id: AF-0119
title: Sntrup761Ring.Decrypt's fallback 'index < W' can become '<= W' with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-09_0647.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Cryptography.UnitLibrary/Sntrup761Ring.cs:Decrypt-lt:surviving-mutant
reproduction: mutation Curl.Cryptography.UnitLibrary/Sntrup761Ring.cs:238:<
task: BL-1869
tasks: BL-1869
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed: 2026-10-09
closed-how: mechanical
closed-by: 2026-10-09_0647.md
---
# AF-0119 - Sntrup761Ring.Decrypt's fallback 'index < W' can become '<= W' with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Cryptography.UnitLibrary/Sntrup761Ring.cs:238`: Sntrup761Ring.Decrypt's fallback 'index < W' can become '<= W' with no test failing. Reported by an auditor flagged unreliable in 2026-10-08_2315.md.

## Evidence

Location: `Curl.Cryptography.UnitLibrary/Sntrup761Ring.cs:238`

Mutant 'int fallback = index < W ? 1 : 0;' -> 'index <= W' survived. The fallback is the fixed-weight vector NTRU Prime returns when the decrypted vector's weight is wrong (mask != 0), i.e. for an invalid ciphertext. The mutant makes it weight W+1, so decapsulating an invalid or tampered ciphertext gives a different implicit-rejection secret than the reference. The Round 3 known-answer test only decapsulates valid ciphertexts, so the rejection path is never compared byte for byte. In SSH the key exchange fails either way, so this is a decision that does not reach output directly.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Cryptography.UnitLibrary/Sntrup761Ring.cs:238:< -Member Decrypt -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | Ran the -Site reproduction: survived Sntrup761Ring.cs:238 < [Decrypt] 'int fallback = index < W ? 1 : 0;' -> '<= W'. It survived in the seed-0 sample of Curl.Cryptography.UnitLibrary as well.
- 2026-10-09 | 2026-10-09_0647.md | reproduces: no | Ran the -Site reproduction for Curl.Cryptography.UnitLibrary/Sntrup761Ring.cs:238:< (member Decrypt): outcome killed. The seed-0 sample of Curl.Cryptography.UnitLibrary also killed this site. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0647.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
