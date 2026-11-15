---
id: AF-0759
title: Oci ReportResponseLineAsync_None_ShowsNothing asserts only that a never-null instance is not null
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces.
key: quality:Curl.Extended.Protocol.OciRegistry.UnitTests/Http/OciHttpHeadReporterTests.cs:ReportResponseLineAsync_None_ShowsNothing:weak-assertion
reproduction: none
task: BL-5208
tasks: BL-5208
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed: 2026-11-15
closed-how: reliable-reaudit
closed-by: 2026-11-15_0222.md
---
# AF-0759 - Oci ReportResponseLineAsync_None_ShowsNothing asserts only that a never-null instance is not null

## Summary

Low finding from the quality auditor at `Curl.Extended.Protocol.OciRegistry.UnitTests/Http/OciHttpHeadReporterTests.cs:111`: Oci ReportResponseLineAsync_None_ShowsNothing asserts only that a never-null instance is not null. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Extended.Protocol.OciRegistry.UnitTests/Http/OciHttpHeadReporterTests.cs:111`

The only assertion is Assert.IsNotNull(OciHttpHeadReporter.None); a never-null instance. Nothing checks that nothing is shown.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.OciRegistry.UnitTests/Http/OciHttpHeadReporterTests.cs -Pattern 'ShowsNothing' -Context 0,6
```

- Expected: An assertion that nothing was reported.
- Actual: Line 111: Assert.IsNotNull(OciHttpHeadReporter.None); is the only check.

## Re-audits

- 2026-11-15 | 2026-11-15_0222.md | reproduces: no | Ran the Select-String. The IsNotNull on a never-null instance is gone; the test asserts Assert.IsTrue(response.IsCompletedSuccessfully). That is still weak for 'ShowsNothing', but the defect as reported no longer reproduces.

## Log

- 2026-11-14: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces.
