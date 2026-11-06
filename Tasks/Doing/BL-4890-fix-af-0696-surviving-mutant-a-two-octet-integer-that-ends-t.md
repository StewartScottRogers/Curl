---
id: BL-4890
title: Fix AF-0696: Surviving mutant: a two-octet integer that ends the input can read as 0
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ObliviousDoh.UnitLibrary]
requirement: none
created: 2026-11-06
completed:
---
# BL-4890 — Fix AF-0696: Surviving mutant: a two-octet integer that ends the input can read as 0

## Goal

The defect the audit office reported as AF-0696 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0696 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0696-surviving-mutant-a-two-octet-integer-that-ends-the.md`.

Location: `Curl.Extension.ObliviousDoh.UnitLibrary/Wire/LengthPrefixedReader.cs:34`

Location: `Curl.Extension.ObliviousDoh.UnitLibrary/Wire/LengthPrefixedReader.cs:34`

'value = remaining.Length < 2 ? (ushort)0 : BinaryPrimitives.ReadUInt16BigEndian(remaining);' mutated to '<= 2' survived (drawn library, seed 982898). With exactly two bytes left, TrySkip(2) still succeeds but the value reads as 0. ObliviousDohConfig reads the version, KEM, KDF and AEAD this way (lines 55 and 73), so a final field could be misread and a config rejected or mis-selected.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.ObliviousDoh.UnitLibrary/Wire/LengthPrefixedReader.cs:34:< -Member TryReadUInt16 -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
