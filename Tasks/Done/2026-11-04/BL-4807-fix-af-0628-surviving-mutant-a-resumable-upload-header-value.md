---
id: BL-4807
title: Fix AF-0628: Surviving mutant: a resumable-upload header value that starts with CR or LF can escape the injection refusal, and no test fails
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary, Curl.Extension.ResumableUpload.UnitTests/Http/ResumableUploadHttpClientConnectionTests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4807 — Fix AF-0628: Surviving mutant: a resumable-upload header value that starts with CR or LF can escape the injection refusal, and no test fails

## Goal

The defect the audit office reported as AF-0628 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0628 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0628-surviving-mutant-a-resumable-upload-header-value-t.md`.

Location: `Curl.Extension.ResumableUpload.UnitLibrary/Http/ResumableUploadHttpClientConnection.cs:51`

Location: `Curl.Extension.ResumableUpload.UnitLibrary/Http/ResumableUploadHttpClientConnection.cs:51`

Invoke-MutationTest -Library Curl.Extension.ResumableUpload.UnitLibrary -MaxMutants 25 -Seed 221035: survived, 'value.AsSpan().IndexOfAny('\r', '\n') >= 0' -> '> 0'. A header value whose first character is CR or LF ('\r\nX-Injected: 1') is then written into the request head and splits it. SendAsync_FieldHoldsCrOrLf_ThrowsArgumentException has only the DataRows ('X-Bad\r\nInjected', 'v') and ('X-Bad', 'v\nInjected: 1'), so the CR or LF is never at index 0 of the value.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.ResumableUpload.UnitLibrary/Http/ResumableUploadHttpClientConnection.cs:51:>= -Member FormatHead -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Test-only fix: SendAsync_FieldHoldsCrOrLf_ThrowsArgumentException gains DataRows with CR or LF at index 0 of the name (`"\r\nX-Injected"`) and of the value (`"\r\nX-Injected: 1"`, `"\nX-Injected: 1"`). Production code is unchanged.
- Touches widened to the test file Curl.Extension.ResumableUpload.UnitTests/Http/ResumableUploadHttpClientConnectionTests.cs, where the missing case belongs. The only other task in Doing, BL-4806, holds Curl.Api.UnitLibrary.
- A lane may not run the audit office mutation tool, so the mutant was applied by hand (">= 0" to "> 0" on the value check at line 51): the two new value rows failed, so the mutant is killed. It was then reverted. The finding closes on the quality auditor re-audit.
- Build clean and fast tests green on Windows. The change is platform-neutral (no paths, OS text or certificates).

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. Header injection test now covers CR or LF at index 0; the '> 0' mutant is killed
