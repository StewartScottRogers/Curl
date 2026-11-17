---
id: AF-0837
title: StartsTheMaxTimeWatchdog test asserts only that OperationStarted is not null
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Engine.UnitTests/ManagedEasyTransferTests.ToolProgress.cs:PerformAsync_NoToolProgressAndATimeout_StartsTheMaxTimeWatchdogAsLibcurlsTimeoutDoes:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0837 - StartsTheMaxTimeWatchdog test asserts only that OperationStarted is not null

## Summary

Low finding from the quality auditor at `Curl.Engine.UnitTests/ManagedEasyTransferTests.ToolProgress.cs:173`: StartsTheMaxTimeWatchdog test asserts only that OperationStarted is not null.

## Evidence

Location: `Curl.Engine.UnitTests/ManagedEasyTransferTests.ToolProgress.cs:173`

The only assertion is Assert.IsNotNull(handler.Contexts.Single().OperationStarted);. Nothing shows a max-time watchdog was started or that it fires: the 1 s timeout never elapses on the SteppingTimeProvider. A perform that started no watchdog but set OperationStarted passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/ManagedEasyTransferTests.ToolProgress.cs -SimpleMatch -Pattern 'Assert.IsNotNull(handler.Contexts.Single().OperationStarted);'
```

- Expected: No match: the test advances the clock past the timeout and asserts OperationTimedOut.
- Actual: ManagedEasyTransferTests.ToolProgress.cs:173: Assert.IsNotNull(handler.Contexts.Single().OperationStarted);

## Re-audits

## Log

- 2026-11-17: filed proposed.
