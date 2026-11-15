---
id: AF-0758
title: Gcs ReportResponseLineAsync_None_ShowsNothing asserts only that a never-null instance is not null
auditor: quality
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Http/GcsHttpHeadReporterTests.cs:ReportResponseLineAsync_None_ShowsNothing:weak-assertion
reproduction: none
task: BL-5207
tasks: BL-5207
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0758 - Gcs ReportResponseLineAsync_None_ShowsNothing asserts only that a never-null instance is not null

## Summary

Low finding from the quality auditor at `Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Http/GcsHttpHeadReporterTests.cs:111`: Gcs ReportResponseLineAsync_None_ShowsNothing asserts only that a never-null instance is not null. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Http/GcsHttpHeadReporterTests.cs:111`

The only assertion is Assert.IsNotNull(GcsHttpHeadReporter.None); GcsHttpHeadReporter.cs:25 is 'public static GcsHttpHeadReporter None => new(NoTransferEvents.Instance, null);', a new expression that can never be null. The 'shows nothing' the name promises is never observed.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Http/GcsHttpHeadReporterTests.cs -Pattern 'ShowsNothing' -Context 0,6
```

- Expected: An assertion that nothing was reported.
- Actual: Line 111: Assert.IsNotNull(GcsHttpHeadReporter.None); is the only check.

## Re-audits

## Log

- 2026-11-14: filed proposed.
