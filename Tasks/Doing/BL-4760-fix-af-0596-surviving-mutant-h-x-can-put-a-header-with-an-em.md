---
id: BL-4760
title: Fix AF-0596: Surviving mutant: -H ':x' can put a header with an empty name into generated code without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.CodeGeneration.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4760 — Fix AF-0596: Surviving mutant: -H ':x' can put a header with an empty name into generated code without a test failing

## Goal

The defect the audit office reported as AF-0596 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0596 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0596-surviving-mutant-h-x-can-put-a-header-with-an-empt.md`.

Location: `Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:134`

Location: `Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:134`

Drawn library. if (colon > 0) -> colon >= 0 survived: a -H line starting with ':' is then added as a field with an empty name, so the generated C# or PowerShell holds a header curl never sends. From_TheOptionsThatSetFields_GiveThoseFieldsUnlessHGaveThem covers 'Host:' and 'NoSeparator' but no leading colon.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.CodeGeneration.UnitLibrary/GeneratedRequest.cs:134:> -Member AddGiven -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
