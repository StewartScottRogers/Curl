---
id: AF-0757
title: AzureStorage ReportResponseLineAsync_None_ShowsNothing asserts only that a never-null instance is not null
auditor: quality
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extended.Protocol.AzureStorage.UnitTests/Http/AzureStorageHttpHeadReporterTests.cs:ReportResponseLineAsync_None_ShowsNothing:weak-assertion
reproduction: none
task: BL-5206
tasks: BL-5206
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0757 - AzureStorage ReportResponseLineAsync_None_ShowsNothing asserts only that a never-null instance is not null

## Summary

Low finding from the quality auditor at `Curl.Extended.Protocol.AzureStorage.UnitTests/Http/AzureStorageHttpHeadReporterTests.cs:111`: AzureStorage ReportResponseLineAsync_None_ShowsNothing asserts only that a never-null instance is not null. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Extended.Protocol.AzureStorage.UnitTests/Http/AzureStorageHttpHeadReporterTests.cs:111`

The only assertion is Assert.IsNotNull(AzureStorageHttpHeadReporter.None); None is a property returning a new instance each read, so it can never be null. Nothing observes whether anything was shown, so a None that wrote to real events or a header stream would still pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.AzureStorage.UnitTests/Http/AzureStorageHttpHeadReporterTests.cs -Pattern 'ShowsNothing' -Context 0,6
```

- Expected: An assertion on recorded events or header output showing that nothing was written.
- Actual: Line 111: Assert.IsNotNull(AzureStorageHttpHeadReporter.None); is the only check.

## Re-audits

## Log

- 2026-11-14: filed proposed.
