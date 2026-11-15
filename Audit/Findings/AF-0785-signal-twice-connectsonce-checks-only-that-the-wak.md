---
id: AF-0785
title: Signal_Twice_ConnectsOnce checks only that the wakeup is readable, not that it connected once
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs:Signal_Twice_ConnectsOnce:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-15
found-at: d04b275ae7a1546a4d0feed894da1ba520945cf5
scorecard: 2026-11-15_0222.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0785 - Signal_Twice_ConnectsOnce checks only that the wakeup is readable, not that it connected once

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs:98`: Signal_Twice_ConnectsOnce checks only that the wakeup is readable, not that it connected once.

## Evidence

Location: `Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs:98`

Step 1 sample. Body: wakeup.Signal(); wakeup.Signal(); Assert.IsTrue(IsReadable(wakeup.Handle, ...)). LoopbackLookupWakeup.Signal guards with 'caller is not null' so a second call does nothing. A second connect would leave the handle just as readable, so the 'Once' in the name is not tested.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs -Pattern 'Signal_Twice_ConnectsOnce' -Context 0,8
```

- Expected: An assertion that exactly one connection reached the listener.
- Actual: Only Assert.IsTrue(IsReadable(wakeup.Handle, TimeSpan.FromSeconds(5))).

## Re-audits

## Log

- 2026-11-15: filed proposed.
