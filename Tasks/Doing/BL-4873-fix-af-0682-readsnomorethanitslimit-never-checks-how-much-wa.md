---
id: BL-4873
title: Fix AF-0682: ReadsNoMoreThanItsLimit never checks how much was read
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OpenTelemetry.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-4873 — Fix AF-0682: ReadsNoMoreThanItsLimit never checks how much was read

## Goal

The defect the audit office reported as AF-0682 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0682 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0682-readsnomorethanitslimit-never-checks-how-much-was.md`.

Location: `Curl.Extension.OpenTelemetry.UnitTests/Otlp/OtlpHttpExporterTests.cs:56`

Location: `Curl.Extension.OpenTelemetry.UnitTests/Otlp/OtlpHttpExporterTests.cs:56`

The test serves 'HTTP/1.1 200 ' followed by 10000 x characters and its only assertion is Assert.IsNull(failure) (line 62). MaxStatusLineLength is 8192 (OtlpHttpExporter.cs:14). An exporter that read the whole 10013 bytes, or one byte past the limit, still parses status 200 and returns null, so the limit the name promises is unchecked. The OpenTelemetry mutation sample confirms it: OtlpHttpExporter.cs:65 'line.Count < MaxStatusLineLength' -> 'line.Count <= MaxStatusLineLength' survived.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenTelemetry.UnitTests/Otlp/OtlpHttpExporterTests.cs -Pattern 'ReadsNoMoreThanItsLimit' -Context 0,7
```

- Expected: The body asserts the number of bytes read from the connection, at most the limit.
- Actual: The only assertion is Assert.IsNull(failure);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
