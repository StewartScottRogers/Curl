---
id: AF-0907
title: Surviving mutant: n-fold can drop the end-around carry into its first byte
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Kerberos.UnitLibrary/KerberosNFold.cs:Fold-ge:surviving-mutant
reproduction: mutation Curl.Kerberos.UnitLibrary/KerberosNFold.cs:33:>=
task: BL-5622
tasks: BL-5622
found: 2026-11-20
found-at: 1ce730765b795e99d4d034faf03432b41b5ec6a4
scorecard: 2026-11-20_0456.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0907 - Surviving mutant: n-fold can drop the end-around carry into its first byte

## Summary

Medium finding from the quality auditor at `Curl.Kerberos.UnitLibrary/KerberosNFold.cs:33`: Surviving mutant: n-fold can drop the end-around carry into its first byte.

## Evidence

Location: `Curl.Kerberos.UnitLibrary/KerberosNFold.cs:33`

Invoke-MutationTest -Library Curl.Kerberos.UnitLibrary -MaxMutants 25 -Seed 912464: 'for (int index = output.Length - 1; index >= 0; index--)' in the ones'-complement wrap-around loop mutated to 'index > 0' survived. The mutant never adds the carry into output[0], so any input whose carry reaches byte 0 folds to a wrong value. The RFC 3961 A.1 vectors and the derive-key vectors all pass with it. n-fold builds every DK key-derivation constant (AesSha1KerberosEncryption.cs:41, Des3CbcSha1KerberosEncryption.cs:104) and folds the whole password and salt in DES3 string-to-key (Des3CbcSha1KerberosEncryption.cs:145). A wrong fold gives a wrong key and a failed authentication.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Kerberos.UnitLibrary/KerberosNFold.cs:33:>= -Member Fold -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-20: filed proposed.
