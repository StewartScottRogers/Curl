---
id: AF-0968
title: EveryEvent_GoesOnToTheInnerEvents checks only that 13 calls arrived, not that each event reached its own method
auditor: quality
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitTests/EngineConnectionIdTransferEventsTests.cs:EveryEvent_GoesOnToTheInnerEvents:weak-assertion
reproduction: none
task: BL-5739
tasks: BL-5739
found: 2026-11-22
found-at: 9f9f37761162551e34c06d99ea4c7ebfb0168e83
scorecard: 2026-11-22_0210.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0968 - EveryEvent_GoesOnToTheInnerEvents checks only that 13 calls arrived, not that each event reached its own method

## Summary

Low finding from the quality auditor at `Curl.Engine.UnitTests/EngineConnectionIdTransferEventsTests.cs:80`: EveryEvent_GoesOnToTheInnerEvents checks only that 13 calls arrived, not that each event reached its own method.

## Evidence

Location: `Curl.Engine.UnitTests/EngineConnectionIdTransferEventsTests.cs:80`

The test reports 13 different events, then its only assertion is 'Assert.AreEqual(13, inner.Count);' (line 80). Its fake CountingEvents (line 93) increments one Count from every method. A wrapper that forwarded ReportDataSent to ReportDataReceived, or passed a different payload, would still give 13. The name promises that every event goes on to the inner events, but the test cannot tell which event went where.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/EngineConnectionIdTransferEventsTests.cs -Pattern 'Assert.AreEqual\(13, inner.Count\)|=> Count\+\+'
```

- Expected: The fake records each call by name and argument, and the test compares that list exactly
- Actual: Line 80: Assert.AreEqual(13, inner.Count); and every CountingEvents method is '=> Count++;'

## Re-audits

## Log

- 2026-11-22: filed proposed.
