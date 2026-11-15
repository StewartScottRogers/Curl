---
id: BL-5207
title: Fix AF-0758: Gcs ReportResponseLineAsync_None_ShowsNothing asserts only that a never-null instance is not null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.GoogleCloudStorage.UnitTests]
requirement: none
created: 2026-11-14
completed: 2026-11-15
---
# BL-5207 — Fix AF-0758: Gcs ReportResponseLineAsync_None_ShowsNothing asserts only that a never-null instance is not null

## Goal

The defect the audit office reported as AF-0758 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0758 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Http/GcsHttpHeadReporterTests.cs:111`

Location: `Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Http/GcsHttpHeadReporterTests.cs:111`

The only assertion is Assert.IsNotNull(GcsHttpHeadReporter.None); GcsHttpHeadReporter.cs:25 is 'public static GcsHttpHeadReporter None => new(NoTransferEvents.Instance, null);', a new expression that can never be null. The 'shows nothing' the name promises is never observed.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Http/GcsHttpHeadReporterTests.cs -Pattern 'ShowsNothing' -Context 0,6
```

- Expected: An assertion that nothing was reported.
- Actual: Line 111: Assert.IsNotNull(GcsHttpHeadReporter.None); is the only check.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Test now takes one instance of `GcsHttpHeadReporter.None`, reports a request head and a response line, and asserts only the reporter's own -w memory changed (HeaderSize 17); None has no header output and no-op events, so nothing is shown. Fast tests: 147 pass. Test-only change, no production code.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-15: Doing -> Done. Test asserts None shows nothing
