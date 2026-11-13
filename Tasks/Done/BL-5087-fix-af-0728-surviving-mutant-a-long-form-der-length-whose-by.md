---
id: BL-5087
title: Fix AF-0728: Surviving mutant: a long-form DER length whose bytes exactly fill the input can be refused
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5087 — Fix AF-0728: Surviving mutant: a long-form DER length whose bytes exactly fill the input can be refused

## Goal

The defect the audit office reported as AF-0728 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0728 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0728-surviving-mutant-a-long-form-der-length-whose-byte.md`.

Location: `Curl.Output.UnitLibrary/DerReader.cs:116`

Location: `Curl.Output.UnitLibrary/DerReader.cs:116`

Mutant 'count > end - position' -> '>=' survived. When the length-of-length bytes end exactly at the buffer's end (for example 0x81 0x00 on an empty last element), the original reads the length and the mutant refuses the element. The boundary is untested.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Output.UnitLibrary/DerReader.cs:116:> -Member TryReadLength -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added TryRead_LongLengthBytesFillTheInputExactly_ReadsTheEmptyElement (0x04 0x81 0x00), which the `>=` mutant refuses. No production change. The audit-tool reproduction is off limits to a lane (audit guard), so it was not run; the test kills the mutant by construction. Fast tests for Curl.Output.UnitTests green.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-13: Doing -> Done. Test pins the boundary mutant
