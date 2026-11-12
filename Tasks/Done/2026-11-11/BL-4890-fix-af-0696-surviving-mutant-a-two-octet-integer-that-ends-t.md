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
completed: 2026-11-06
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The mutant (`< 2` to `<= 2` in `TryReadUInt16`) survived because no test read a two-octet integer with exactly two bytes left. Added `Curl.Extension.ObliviousDoh.UnitTests/Wire/LengthPrefixedReaderTests.cs`: `TryReadUInt16_ExactlyTwoBytesLeft_ReadsTheBigEndianValueAndEmptiesTheReader` asserts 0x1234 from `[0x12, 0x34]`, which the mutant reads as 0, so it is killed. Production code is unchanged, so no quality run was needed.
- A lane cannot run `Audit/Tools/Invoke-MutationTest.ps1` (audit paths are refused) and applying the mutant by hand was not permitted in this run, so the kill was checked by reading the code; the quality auditor's re-audit confirms it.
- `dotnet build` clean (0 warnings) and the fast tests green on Windows. The tests are pure in-memory byte reads, so the result does not depend on the platform.

## Log

- 2026-11-06: Created.
- 2026-11-06: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-06: Doing -> Done. Unit tests now read a two-octet integer that ends the input, killing the <= 2 mutant
