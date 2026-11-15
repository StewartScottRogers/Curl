---
id: BL-5206
title: Fix AF-0757: AzureStorage ReportResponseLineAsync_None_ShowsNothing asserts only that a never-null instance is not null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitTests]
requirement: none
created: 2026-11-14
completed:
---
# BL-5206 — Fix AF-0757: AzureStorage ReportResponseLineAsync_None_ShowsNothing asserts only that a never-null instance is not null

## Goal

The defect the audit office reported as AF-0757 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0757 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.AzureStorage.UnitTests/Http/AzureStorageHttpHeadReporterTests.cs:111`

Location: `Curl.Extended.Protocol.AzureStorage.UnitTests/Http/AzureStorageHttpHeadReporterTests.cs:111`

The only assertion is Assert.IsNotNull(AzureStorageHttpHeadReporter.None); None is a property returning a new instance each read, so it can never be null. Nothing observes whether anything was shown, so a None that wrote to real events or a header stream would still pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.AzureStorage.UnitTests/Http/AzureStorageHttpHeadReporterTests.cs -Pattern 'ShowsNothing' -Context 0,6
```

- Expected: An assertion on recorded events or header output showing that nothing was written.
- Actual: Line 111: Assert.IsNotNull(AzureStorageHttpHeadReporter.None); is the only check.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
