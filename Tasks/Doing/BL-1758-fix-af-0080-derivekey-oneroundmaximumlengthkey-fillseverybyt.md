---
id: BL-1758
title: Fix AF-0080: DeriveKey_OneRoundMaximumLengthKey_FillsEveryByte ORs three bytes together, so a key filled only at byte 0 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1758 — Fix AF-0080: DeriveKey_OneRoundMaximumLengthKey_FillsEveryByte ORs three bytes together, so a key filled only at byte 0 passes

## Goal

The defect the audit office reported as AF-0080 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0080 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0080-derivekey-oneroundmaximumlengthkey-fillseverybyte.md`.

Location: `Curl.Cryptography.UnitTests/BcryptPbkdfTests.cs:107`

Location: `Curl.Cryptography.UnitTests/BcryptPbkdfTests.cs:107`

Line 107: 'int probe = key[^1] | key[0] | key[31];' and line 111: 'Assert.AreNotEqual(0, probe);'. The name promises every byte of the maximum-length key is filled. The OR passes whenever any one of those three bytes is non-zero, so a derivation that leaves the tail of the key zero passes if key[0] is non-zero. No published vector or full-key comparison pins the output.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitTests/BcryptPbkdfTests.cs -Pattern 'int probe|AreNotEqual\(0, probe\)'
```

- Expected: The test compares the whole key with a reference, or checks every byte or every output block.
- Actual: BcryptPbkdfTests.cs:107: int probe = key[^1] | key[0] | key[31]; BcryptPbkdfTests.cs:111: Assert.AreNotEqual(0, probe);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
