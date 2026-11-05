---
id: BL-4869
title: Fix AF-0678: Surviving mutant: an HTTP 400 response can be exported as a successful span
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4873]
touches: [Curl.Extension.OpenTelemetry.UnitLibrary, Curl.Extension.OpenTelemetry.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-4869 — Fix AF-0678: Surviving mutant: an HTTP 400 response can be exported as a successful span

## Goal

The defect the audit office reported as AF-0678 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0678 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0678-surviving-mutant-an-http-400-response-can-be-expor.md`.

Location: `Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpClientSpan.cs:69`

Location: `Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpClientSpan.cs:69`

Mutant ': statusCode >= 400 ? statusCode.ToString(...)' -> ': statusCode > 400 ? ...' survived in the sample and in the targeted -Site run. The only status test (For_AStatusOf400OrMore_IsAnErrorWithTheStatusAsItsType) uses 404, so the 400 boundary is unchecked. With the mutant, a 400 response has no error.type attribute and no error status in the OTLP span POSTed to the collector: the exported request bytes change.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpClientSpan.cs:69:>= -Member ErrorTypeOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant is killed.
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-11-05 (lane 6): The fix is a test pinning the 400 boundary (a 400 response is an error with error.type "400") in `Curl.Extension.OpenTelemetry.UnitTests/Otlp/OtlpHttpClientSpanTests.cs`; the production code is already right. That test project was not in touches and BL-4873 (in Doing) holds it whole, so touches was widened to it and BL-4873 added to depends-on; requeued before any code edit.

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-05: Doing -> Backlog. Needs Curl.Extension.OpenTelemetry.UnitTests, held by BL-4873 in Doing; waits on BL-4873
