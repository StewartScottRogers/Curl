---
id: BL-5209
title: Fix AF-0760: S3 ReportResponseLineAsync_None_ShowsNothing asserts only that a never-null instance is not null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.S3.UnitTests]
requirement: none
created: 2026-11-14
completed:
---
# BL-5209 — Fix AF-0760: S3 ReportResponseLineAsync_None_ShowsNothing asserts only that a never-null instance is not null

## Goal

The defect the audit office reported as AF-0760 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0760 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.S3.UnitTests/Http/S3HttpHeadReporterTests.cs:111`

Location: `Curl.Extended.Protocol.S3.UnitTests/Http/S3HttpHeadReporterTests.cs:111`

The only assertion is Assert.IsNotNull(S3HttpHeadReporter.None); a never-null instance. Nothing checks that nothing is shown.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.S3.UnitTests/Http/S3HttpHeadReporterTests.cs -Pattern 'ShowsNothing' -Context 0,6
```

- Expected: An assertion that nothing was reported.
- Actual: Line 111: Assert.IsNotNull(S3HttpHeadReporter.None); is the only check.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
