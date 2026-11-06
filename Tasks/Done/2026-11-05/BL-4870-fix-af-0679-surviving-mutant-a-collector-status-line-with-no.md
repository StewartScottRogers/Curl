---
id: BL-4870
title: Fix AF-0679: Surviving mutant: a collector status line with no reason phrase can be taken as no HTTP response
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4873]
touches: [Curl.Extension.OpenTelemetry.UnitLibrary, Curl.Extension.OpenTelemetry.UnitTests/Otlp/OtlpHttpExporterTests.cs]
requirement: none
created: 2026-11-05
completed: 2026-11-05
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-11-05 (lane 6): The mutant is killed by a test in `Curl.Extension.OpenTelemetry.UnitTests/Otlp/OtlpHttpExporterTests.cs` exporting against a collector answering `HTTP/1.1 200` (no reason phrase) and expecting success; no production change is needed. That project is held by BL-4873 (in Doing), so the test file was added to touches, BL-4873 to depends-on, and the task requeued with no edits made.

- 2026-11-05 (lane 2): Added `ExportAsync_ACollectorWhoseStatusLineHasNoReasonPhrase_Succeeds` (collector answers `HTTP/1.1 200`, expects success), which kills the `>= 2` -> `> 2` mutant at OtlpHttpExporter.cs:76: with the mutant that line splits into 2 parts and is reported as "no HTTP response". Test-only change, no production code, so no quality run was needed. A lane may not run the mutation tool, so the first criterion is ticked on that reasoning; the re-audit confirms it. Platform-neutral (no paths, OS errors or certificates).

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-05: Doing -> Backlog. Waits on BL-4873, which holds Curl.Extension.OpenTelemetry.UnitTests where the killing test goes
- 2026-11-05: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-05: Doing -> Done. Test kills the >= 2 mutant at OtlpHttpExporter.cs:76
