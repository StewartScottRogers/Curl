---
id: AF-0837
title: StartsTheMaxTimeWatchdog test asserts only that OperationStarted is not null
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces.
key: quality:Curl.Engine.UnitTests/ManagedEasyTransferTests.ToolProgress.cs:PerformAsync_NoToolProgressAndATimeout_StartsTheMaxTimeWatchdogAsLibcurlsTimeoutDoes:weak-assertion
reproduction: none
task: BL-5357
tasks: BL-5357
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed: 2026-11-19
closed-how: reliable-reaudit
closed-by: 2026-11-19_0134.md
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

- 2026-11-18 | 2026-11-18_0810.md | reproduces: no | Ran the Select-String: no match for 'Assert.IsNotNull(handler.Contexts.Single().OperationStarted);'. The test (line 165) now asserts Assert.AreEqual(CurlCode.OperationTimedOut, code) after advancing the clock past the 1 s timeout.
- 2026-11-19 | 2026-11-19_0134.md | reproduces: no | Select-String for 'Assert.IsNotNull(handler.Contexts.Single().OperationStarted);' in ManagedEasyTransferTests.ToolProgress.cs finds no match.

## Log

- 2026-11-17: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces.
