---
id: AF-0985
title: Lib5000And5004_SetTheSigningOptionsOnTheTransfer checks the signing key and key id only for null
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-11-25_1306.md: the reproduction no longer reproduces.
key: quality:Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs:Lib5000And5004_SetTheSigningOptionsOnTheTransfer:weak-assertion
reproduction: none
task: BL-5787
tasks: BL-5787
found: 2026-11-22
found-at: 94c9744bc919995aff928a0378582601ddbbe990
scorecard: 2026-11-22_1817.md
duplicate-of:
closed: 2026-11-25
closed-how: reliable-reaudit
closed-by: 2026-11-25_1306.md
---
# AF-0985 - Lib5000And5004_SetTheSigningOptionsOnTheTransfer checks the signing key and key id only for null

## Summary

Low finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs:348`: Lib5000And5004_SetTheSigningOptionsOnTheTransfer checks the signing key and key id only for null.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs:348`

Step 1 (seeded sample of 50). The test pins the algorithm (Assert.AreEqual("ed25519", seen!.Authentication.HttpSignatureAlgorithm)), but the key and key id it says are set get only Assert.IsNotNull(seen.Authentication.HttpSignatureKey) and Assert.IsNotNull(seen.Authentication.HttpSignatureKeyId). The ports hard-code upstream lib5000/lib5004's key and key id, so a port that passed a wrong key or key id would still pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib3101To5004Tests.cs -Pattern 'IsNotNull\(seen.Authentication.HttpSignature'
```

- Expected: No match: the key and key id are compared with the values lib5000.c and lib5004.c set.
- Actual: Lib3101To5004Tests.cs:348 Assert.IsNotNull(seen.Authentication.HttpSignatureKey); Lib3101To5004Tests.cs:349 Assert.IsNotNull(seen.Authentication.HttpSignatureKeyId);

## Re-audits

- 2026-11-25 | 2026-11-25_1306.md | reproduces: no | Select-String for 'IsNotNull\(seen.Authentication.HttpSignature' found no match; lines 347-349 assert the algorithm, the key 9f8362f8...29c5 and the key id test-key-ed25519 exactly.

## Log

- 2026-11-22: filed proposed.
- 2026-11-25: accepted -> closed. Re-audit 2026-11-25_1306.md: the reproduction no longer reproduces.
