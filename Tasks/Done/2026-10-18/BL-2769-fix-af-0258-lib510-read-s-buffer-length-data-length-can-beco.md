---
id: BL-2769
title: Fix AF-0258: Lib510 Read's buffer.Length < data.Length can become <= with no test failing, refusing a buffer that fits exactly
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2769 — Fix AF-0258: Lib510 Read's buffer.Length < data.Length can become <= with no test failing, refusing a buffer that fits exactly

## Goal

The defect the audit office reported as AF-0258 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0258 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0258-lib510-read-s-buffer-length-data-length-can-become.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib510.cs:88`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib510.cs:88`

LibTest sample, seed 0: survived. Original `if (buffer.Length < data.Length)`, mutated `<=`. A read buffer exactly as long as the part would be refused with 'read buffer is too small to run test', and the upload would end early. No test covers the exact-fit edge.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib510.cs:88:< -Member Read -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added Curl.Conformance.LibTest.UnitTests to `touches`: the fix is a test (exact-fit buffer for PartReader.Read), which lives there; no other Doing task names it. Production code is unchanged, so no quality run was needed.
- Lanes may not run `Audit/Tools/Invoke-MutationTest.ps1` (audit guard), so the mutant was checked by hand: with `<=` in Lib510.cs:88, `PartReader_BufferExactlyThePartsLength_CopiesThePart` fails (1 of 4 Lib510Tests); with `<` restored all pass. The re-audit runs the real reproduction.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-18: Doing -> Done. Exact-fit buffer test kills the <= mutant at Lib510.cs:88
