---
id: BL-4811
title: Fix AF-0632: Surviving mutant: lib552's write callback can print a whole chunk that starts with NUL instead of nothing, and no test fails
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4811 — Fix AF-0632: Surviving mutant: lib552's write callback can print a whole chunk that starts with NUL instead of nothing, and no test fails

## Goal

The defect the audit office reported as AF-0632 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0632 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0632-surviving-mutant-lib552-s-write-callback-can-print.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:96`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:96`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'standardOutput.Write(end < 0 ? data : data[..end]);' -> 'end <= 0'. The doc comment says it 'prints the bytes up to the first NUL, as %.*s does'. For a chunk whose first byte is NUL the mutant prints the whole chunk, so stdout differs from upstream's. No test writes a chunk starting with NUL.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib552.cs:96:< -Member Write -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Lib552Tests.DataBuffer_ReadsTheDataWritesUpToANulAndRewinds now also writes the chunk "## Notesgh" and still expects stdout "...abef", so the `end <= 0` mutant (which prints the whole chunk) fails it. A lane cannot run the mutation tool, and a direct mutant run was refused in this session, so the kill was checked by reasoning; the re-audit confirms it. Build clean; fast tests green.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. Lib552's write test now writes a chunk starting with NUL, killing the end <= 0 mutant
