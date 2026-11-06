---
id: AF-0682
title: ReadsNoMoreThanItsLimit never checks how much was read
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-05_2025.md: the reproduction no longer reproduces.
key: quality:Curl.Extension.OpenTelemetry.UnitTests/Otlp/OtlpHttpExporterTests.cs:ExportAsync_AStatusLineThatNeverEnds_ReadsNoMoreThanItsLimit:name-lies
reproduction: none
task: BL-4873
tasks: BL-4873
found: 2026-11-05
found-at: aea9f0110c59ad5209aafb9f0f985886a2cb9439
scorecard: 2026-11-05_0819.md
duplicate-of:
closed: 2026-11-05
closed-how: reliable-reaudit
closed-by: 2026-11-05_2025.md
---
# AF-0682 - ReadsNoMoreThanItsLimit never checks how much was read

## Summary

Medium finding from the quality auditor at `Curl.Extension.OpenTelemetry.UnitTests/Otlp/OtlpHttpExporterTests.cs:56`: ReadsNoMoreThanItsLimit never checks how much was read.

## Evidence

Location: `Curl.Extension.OpenTelemetry.UnitTests/Otlp/OtlpHttpExporterTests.cs:56`

The test serves 'HTTP/1.1 200 ' followed by 10000 x characters and its only assertion is Assert.IsNull(failure) (line 62). MaxStatusLineLength is 8192 (OtlpHttpExporter.cs:14). An exporter that read the whole 10013 bytes, or one byte past the limit, still parses status 200 and returns null, so the limit the name promises is unchecked. The OpenTelemetry mutation sample confirms it: OtlpHttpExporter.cs:65 'line.Count < MaxStatusLineLength' -> 'line.Count <= MaxStatusLineLength' survived.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.OpenTelemetry.UnitTests/Otlp/OtlpHttpExporterTests.cs -Pattern 'ReadsNoMoreThanItsLimit' -Context 0,7
```

- Expected: The body asserts the number of bytes read from the connection, at most the limit.
- Actual: The only assertion is Assert.IsNull(failure);

## Re-audits

- 2026-11-05 | 2026-11-05_2025.md | reproduces: no | Ran the Select-String reproduction: the test now ends with Assert.IsNull(failure); Assert.AreEqual(8192, connection.BytesRead); (OtlpHttpExporterTests.cs:72-73), so it checks how much was read.

## Log

- 2026-11-05: filed proposed.
- 2026-11-05: accepted -> closed. Re-audit 2026-11-05_2025.md: the reproduction no longer reproduces.
