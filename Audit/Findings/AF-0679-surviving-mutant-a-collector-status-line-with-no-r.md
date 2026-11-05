---
id: AF-0679
title: Surviving mutant: a collector status line with no reason phrase can be taken as no HTTP response
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpExporter.cs:Outcome-ge:surviving-mutant
reproduction: mutation Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpExporter.cs:76:>=
task: none
tasks:
found: 2026-11-05
found-at: aea9f0110c59ad5209aafb9f0f985886a2cb9439
scorecard: 2026-11-05_0819.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0679 - Surviving mutant: a collector status line with no reason phrase can be taken as no HTTP response

## Summary

Medium finding from the quality auditor at `Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpExporter.cs:76`: Surviving mutant: a collector status line with no reason phrase can be taken as no HTTP response.

## Evidence

Location: `Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpExporter.cs:76`

Mutant 'return parts.Length >= 2 && parts[0].StartsWith("HTTP/", ...)' -> 'parts.Length > 2 && ...' survived in the sample and in the targeted -Site run. Every exporter test's status line has a reason phrase ("HTTP/1.1 200 OK", "HTTP/1.1 202 Accepted", "HTTP/1.1 503 Service Unavailable"), so a valid 'HTTP/1.1 200' line, which splits into 2 parts, is never tried. With the mutant that export is reported as 'the collector sent no HTTP response' and logged as a warning.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpExporter.cs:76:>= -Member Outcome -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant is killed.
- Actual: survived

## Re-audits

## Log

- 2026-11-05: filed proposed.
