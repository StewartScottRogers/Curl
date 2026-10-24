---
id: AF-0402
title: Of_VerboseWithADebugCallback_GivesTheEvents asserts only that a result is not null
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Engine.UnitTests/DebugCallbackTransferEventsTests.cs:Of_VerboseWithADebugCallback_GivesTheEvents:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0402 - Of_VerboseWithADebugCallback_GivesTheEvents asserts only that a result is not null

## Summary

Low finding from the quality auditor at `Curl.Engine.UnitTests/DebugCallbackTransferEventsTests.cs:34`: Of_VerboseWithADebugCallback_GivesTheEvents asserts only that a result is not null. Reported by an auditor flagged unreliable in 2026-10-23_2322.md.

## Evidence

Location: `Curl.Engine.UnitTests/DebugCallbackTransferEventsTests.cs:34`

The whole body is Assert.IsNotNull(DebugCallbackTransferEvents.Of(verbose: true, (_, _) => 0));. The name promises the debug-callback events are given; any non-null ITransferEvents - including one that ignores the callback - passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/DebugCallbackTransferEventsTests.cs -Pattern 'Of_VerboseWithADebugCallback_GivesTheEvents' -Context 0,3
```

- Expected: The test asserts the returned events' type or that a reported event reaches the callback.
- Actual: DebugCallbackTransferEventsTests.cs:34:        Assert.IsNotNull(DebugCallbackTransferEvents.Of(verbose: true, (_, _) => 0));

## Re-audits

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
