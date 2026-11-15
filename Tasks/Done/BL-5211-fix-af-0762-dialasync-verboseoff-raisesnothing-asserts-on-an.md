---
id: BL-5211
title: Fix AF-0762: DialAsync_VerboseOff_RaisesNothing asserts on an event recorder that is never passed to the dial, so it can never fail
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.WebTransport.UnitTests]
requirement: none
created: 2026-11-14
completed: 2026-11-14
---
# BL-5211 — Fix AF-0762: DialAsync_VerboseOff_RaisesNothing asserts on an event recorder that is never passed to the dial, so it can never fail

## Goal

The defect the audit office reported as AF-0762 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0762 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportVerboseTests.cs:55`

Location: `Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportVerboseTests.cs:55`

Line 51 creates 'RecordingEvents unattached = new();' and line 55 asserts Assert.IsEmpty(unattached.Order), but the dial at line 53 is DialAsync(Request, CancellationToken.None), the overload without events. The recorder is never connected to anything, so the assertion is true whatever the dial raises. A dial that raised handshake and opened events with verbose off would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportVerboseTests.cs -Pattern 'DialAsync_VerboseOff_RaisesNothing' -Context 0,8
```

- Expected: The dial's events go somewhere the test observes (or the global sink is checked), and that is asserted empty.
- Actual: unattached is created, never passed to DialAsync, then asserted empty.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Renamed the test to DialAsync_VerboseOff_DialsWithTheSilentSinkAndStillConnects: NoTransferEvents.Instance is a stateless global sink nothing can observe, so the test now asserts the handler hands the dial that sink by default and that the no-events overload still connects. Fast tests of the WebTransport test project: 87 passed. Platform-neutral.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-14: Doing -> Done. Test now asserts the dial's default silent sink
