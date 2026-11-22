---
id: BL-5739
title: Fix AF-0968: EveryEvent_GoesOnToTheInnerEvents checks only that 13 calls arrived, not that each event reached its own method
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests/EngineConnectionIdTransferEventsTests.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5739 — Fix AF-0968: EveryEvent_GoesOnToTheInnerEvents checks only that 13 calls arrived, not that each event reached its own method

## Goal

The defect the audit office reported as AF-0968 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0968 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitTests/EngineConnectionIdTransferEventsTests.cs:80`

Location: `Curl.Engine.UnitTests/EngineConnectionIdTransferEventsTests.cs:80`

The test reports 13 different events, then its only assertion is 'Assert.AreEqual(13, inner.Count);' (line 80). Its fake CountingEvents (line 93) increments one Count from every method. A wrapper that forwarded ReportDataSent to ReportDataReceived, or passed a different payload, would still give 13. The name promises that every event goes on to the inner events, but the test cannot tell which event went where.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/EngineConnectionIdTransferEventsTests.cs -Pattern 'Assert.AreEqual\(13, inner.Count\)|=> Count\+\+'
```

- Expected: The fake records each call by name and argument, and the test compares that list exactly
- Actual: Line 80: Assert.AreEqual(13, inner.Count); and every CountingEvents method is '=> Count++;'

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
