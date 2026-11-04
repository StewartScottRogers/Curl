---
id: BL-4843
title: Fix AF-0661: Surviving mutant: lib547's ioctl callback can rewind on every command except RestartRead
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4843 — Fix AF-0661: Surviving mutant: lib547's ioctl callback can rewind on every command except RestartRead

## Goal

The defect the audit office reported as AF-0661 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0661 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0661-surviving-mutant-lib547-s-ioctl-callback-can-rewin.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:103`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:103`

Mutant 'command == IoctlCommand.RestartRead' -> '!=' survived. REWIND! would be written and the upload counter reset on the wrong commands, and not on a real restart. That changes stderr and the bytes uploaded. No test fails.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib547.cs:103:== -Member Ioctl -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
