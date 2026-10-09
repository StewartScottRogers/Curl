---
id: AF-0080
title: DeriveKey_OneRoundMaximumLengthKey_FillsEveryByte ORs three bytes together, so a key filled only at byte 0 passes
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-09_0225.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_2315.md, 2026-10-09_0225.md).
key: quality:Curl.Cryptography.UnitTests/BcryptPbkdfTests.cs:DeriveKey_OneRoundMaximumLengthKey_FillsEveryByte:name-lies
reproduction: none
task: BL-1758
tasks: BL-1758
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed: 2026-10-09
closed-how: consecutive
closed-by: 2026-10-08_2315.md, 2026-10-09_0225.md
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

- 2026-10-08 | 2026-10-08_2315.md | reproduces: no | Ran the Select-String: no match for 'int probe' or 'AreNotEqual(0, probe)'. The test is now DeriveKey_OneRoundMaximumLengthKey_EqualsTheInterleavedBlockHashes (BcryptPbkdfTests.cs:101), which builds the whole 1024-byte expected key from the interleaved block hashes and asserts Assert.AreEqual on the full hex of every byte.
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | The Select-String pattern 'int probe|AreNotEqual\(0, probe\)' finds no match. The test is now DeriveKey_OneRoundMaximumLengthKey_EqualsTheInterleavedBlockHashes (BcryptPbkdfTests.cs:101). It compares the hex of every byte of the 1024-byte key with a reference built from ComputeHash (Assert.AreEqual of the two hex strings).

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0225.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_2315.md, 2026-10-09_0225.md).
