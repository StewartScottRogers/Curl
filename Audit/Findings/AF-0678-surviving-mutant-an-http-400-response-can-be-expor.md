---
id: AF-0678
title: Surviving mutant: an HTTP 400 response can be exported as a successful span
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-05_2025.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpClientSpan.cs:ErrorTypeOf-ge:surviving-mutant
reproduction: mutation Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpClientSpan.cs:69:>=
task: BL-4869
tasks: BL-4869
found: 2026-11-05
found-at: aea9f0110c59ad5209aafb9f0f985886a2cb9439
scorecard: 2026-11-05_0819.md
duplicate-of:
closed: 2026-11-05
closed-how: mechanical
closed-by: 2026-11-05_2025.md
---
# AF-0678 - Surviving mutant: an HTTP 400 response can be exported as a successful span

## Summary

High finding from the quality auditor at `Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpClientSpan.cs:69`: Surviving mutant: an HTTP 400 response can be exported as a successful span.

## Evidence

Location: `Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpClientSpan.cs:69`

Mutant ': statusCode >= 400 ? statusCode.ToString(...)' -> ': statusCode > 400 ? ...' survived in the sample and in the targeted -Site run. The only status test (For_AStatusOf400OrMore_IsAnErrorWithTheStatusAsItsType) uses 404, so the 400 boundary is unchecked. With the mutant, a 400 response has no error.type attribute and no error status in the OTLP span POSTed to the collector: the exported request bytes change.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpClientSpan.cs:69:>= -Member ErrorTypeOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant is killed.
- Actual: survived

## Re-audits

- 2026-11-05 | 2026-11-05_2025.md | reproduces: no | Ran the -Site reproduction: ': statusCode >= 400 ? ...' -> '> 400', outcome killed (resolvedLine 69). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-05: filed proposed.
- 2026-11-05: accepted -> closed. Re-audit 2026-11-05_2025.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
