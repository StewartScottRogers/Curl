---
id: BL-4810
title: Fix AF-0631: Surviving mutant: lib650 can send its second field's name one byte longer (NameLength +1 instead of -1), and no test fails
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4810 — Fix AF-0631: Surviving mutant: lib650 can send its second field's name one byte longer (NameLength +1 instead of -1), and no test fails

## Goal

The defect the audit office reported as AF-0631 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0631 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0631-surviving-mutant-lib650-can-send-its-second-field.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:96`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:96`

Invoke-MutationTest -Library Curl.Conformance.LibTest.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'new(CurlFormOption.NameLength, (long)TestName.Length - 1)' -> '+ 1'. Upstream test650 sends the second part under the name cut by one byte (CURLFORM_NAMELENGTH). The mutant changes the multipart request bytes the port builds, and the LibTest tests never compare that part's name.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib650.cs:96:-1 -Member BuildForm -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
