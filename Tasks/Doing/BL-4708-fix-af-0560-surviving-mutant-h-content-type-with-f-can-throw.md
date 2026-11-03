---
id: BL-4708
title: Fix AF-0560: Surviving mutant: -H Content-Type with -F can throw IndexOutOfRangeException without a test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4708 — Fix AF-0560: Surviving mutant: -H Content-Type with -F can throw IndexOutOfRangeException without a test failing

## Goal

The defect the audit office reported as AF-0560 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0560 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0560-surviving-mutant-h-content-type-with-f-can-throw-i.md`.

Location: `Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:66`

Location: `Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:66`

Mutant 'header.Length > ContentTypeName.Length' -> '>=' survived. With it, a header of exactly 'Content-Type' reaches 'header[ContentTypeName.Length]', one past its end, and the form build throws instead of ignoring the header. No test passes an -H value equal to the bare name.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:66:> -Member RequestContentTypeOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
