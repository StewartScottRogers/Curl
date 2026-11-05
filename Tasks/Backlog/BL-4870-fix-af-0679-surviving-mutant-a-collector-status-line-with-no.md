---
id: BL-4870
title: Fix AF-0679: Surviving mutant: a collector status line with no reason phrase can be taken as no HTTP response
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OpenTelemetry.UnitLibrary]
requirement: none
created: 2026-11-05
completed:
---
# BL-4870 — Fix AF-0679: Surviving mutant: a collector status line with no reason phrase can be taken as no HTTP response

## Goal

The defect the audit office reported as AF-0679 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0679 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0679-surviving-mutant-a-collector-status-line-with-no-r.md`.

Location: `Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpExporter.cs:76`

Location: `Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpExporter.cs:76`

Mutant 'return parts.Length >= 2 && parts[0].StartsWith("HTTP/", ...)' -> 'parts.Length > 2 && ...' survived in the sample and in the targeted -Site run. Every exporter test's status line has a reason phrase ("HTTP/1.1 200 OK", "HTTP/1.1 202 Accepted", "HTTP/1.1 503 Service Unavailable"), so a valid 'HTTP/1.1 200' line, which splits into 2 parts, is never tried. With the mutant that export is reported as 'the collector sent no HTTP response' and logged as a warning.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.OpenTelemetry.UnitLibrary/Otlp/OtlpHttpExporter.cs:76:>= -Member Outcome -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant is killed.
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-05: Created.
