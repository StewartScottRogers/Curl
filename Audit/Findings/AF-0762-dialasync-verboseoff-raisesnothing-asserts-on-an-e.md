---
id: AF-0762
title: DialAsync_VerboseOff_RaisesNothing asserts on an event recorder that is never passed to the dial, so it can never fail
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportVerboseTests.cs:DialAsync_VerboseOff_RaisesNothing:weak-assertion
reproduction: none
task: BL-5211
tasks: BL-5211
found: 2026-11-14
found-at: 92f6fc980babe08d1ebff5f02d7e076e42c67fd7
scorecard: 2026-11-14_1801.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0762 - DialAsync_VerboseOff_RaisesNothing asserts on an event recorder that is never passed to the dial, so it can never fail

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportVerboseTests.cs:55`: DialAsync_VerboseOff_RaisesNothing asserts on an event recorder that is never passed to the dial, so it can never fail. Reported by an auditor flagged unreliable in 2026-11-14_1801.md.

## Evidence

Location: `Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportVerboseTests.cs:55`

Line 51 creates 'RecordingEvents unattached = new();' and line 55 asserts Assert.IsEmpty(unattached.Order), but the dial at line 53 is DialAsync(Request, CancellationToken.None), the overload without events. The recorder is never connected to anything, so the assertion is true whatever the dial raises. A dial that raised handshake and opened events with verbose off would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extended.Protocol.WebTransport.UnitTests/WebTransportVerboseTests.cs -Pattern 'DialAsync_VerboseOff_RaisesNothing' -Context 0,8
```

- Expected: The dial's events go somewhere the test observes (or the global sink is checked), and that is asserted empty.
- Actual: unattached is created, never passed to DialAsync, then asserted empty.

## Re-audits

## Log

- 2026-11-14: filed proposed.
