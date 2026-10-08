---
id: AF-0080
title: DeriveKey_OneRoundMaximumLengthKey_FillsEveryByte ORs three bytes together, so a key filled only at byte 0 passes
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Cryptography.UnitTests/BcryptPbkdfTests.cs:DeriveKey_OneRoundMaximumLengthKey_FillsEveryByte:name-lies
reproduction: none
task: none
tasks:
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0080 - DeriveKey_OneRoundMaximumLengthKey_FillsEveryByte ORs three bytes together, so a key filled only at byte 0 passes

## Summary

Medium finding from the quality auditor at `Curl.Cryptography.UnitTests/BcryptPbkdfTests.cs:107`: DeriveKey_OneRoundMaximumLengthKey_FillsEveryByte ORs three bytes together, so a key filled only at byte 0 passes.

## Evidence

Location: `Curl.Cryptography.UnitTests/BcryptPbkdfTests.cs:107`

Line 107: 'int probe = key[^1] | key[0] | key[31];' and line 111: 'Assert.AreNotEqual(0, probe);'. The name promises every byte of the maximum-length key is filled. The OR passes whenever any one of those three bytes is non-zero, so a derivation that leaves the tail of the key zero passes if key[0] is non-zero. No published vector or full-key comparison pins the output.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitTests/BcryptPbkdfTests.cs -Pattern 'int probe|AreNotEqual\(0, probe\)'
```

- Expected: The test compares the whole key with a reference, or checks every byte or every output block.
- Actual: BcryptPbkdfTests.cs:107: int probe = key[^1] | key[0] | key[31]; BcryptPbkdfTests.cs:111: Assert.AreNotEqual(0, probe);

## Re-audits

## Log

- 2026-10-08: filed proposed.
