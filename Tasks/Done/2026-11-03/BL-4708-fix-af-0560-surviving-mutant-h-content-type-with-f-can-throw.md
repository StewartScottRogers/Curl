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
completed: 2026-11-03
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added three `RequestContentTypeOf` tests to `Curl.Engine.UnitTests/MultipartFormPartMappingTests.cs` (the test project of the touched library; no production change, so no quality run was needed): a bare `Content-Type` value is skipped and the next one read, a bare name or `Content-Types:` gives null, and `Content-Type:` gives empty. A lane cannot run `Audit/Tools/Invoke-MutationTest.ps1`, so I applied the mutant (`>` to `>=` at line 66) by hand: two of the new tests failed against it, then I reverted it. The re-audit confirms. Engine fast tests: 2957 passed, 0 failed.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. Tests for a bare Content-Type -H value kill the >= mutant at MultipartFormPartMapping.cs:66; engine fast tests green
