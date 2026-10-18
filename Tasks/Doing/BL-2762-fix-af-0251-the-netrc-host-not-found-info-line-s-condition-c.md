---
id: BL-2762
title: Fix AF-0251: The netrc 'host not found' info line's condition can flip (!= to ==) with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-18
completed:
---
# BL-2762 — Fix AF-0251: The netrc 'host not found' info line's condition can flip (!= to ==) with no test failing

## Goal

The defect the audit office reported as AF-0251 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0251 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0251-the-netrc-host-not-found-info-line-s-condition-can.md`.

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:142`

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:142`

Engine sample, seed 0: survived. Original `if (failure is null && result.Outcome != NetrcLookupOutcome.Found)`, mutated `==`. The mutant writes HostNotFoundLine when the host was found and leaves it out when it was not, so the -v standard-error bytes change in both cases. No test pins the line on the engine path.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:142:!= -Member TryLookUpInNetrcFile -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
