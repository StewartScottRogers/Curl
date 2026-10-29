---
id: AF-0460
title: Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing has no assertion, so the listener's closing is never checked
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs:Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing:no-assertion
reproduction: none
task: BL-4241
tasks: BL-4241
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0460 - Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing has no assertion, so the listener's closing is never checked

## Summary

Low finding from the quality auditor at `Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs:109`: Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing has no assertion, so the listener's closing is never checked. Reported by an auditor flagged unreliable in 2026-10-29_0606.md.

## Evidence

Location: `Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs:109`

Found by Find-WeakTests.ps1 (no-assertion). The body is `LoopbackLookupWakeup wakeup = LoopbackLookupWakeup.Open(); wakeup.Dispose(); wakeup.Signal();` with no Assert of any kind. The name promises `ClosesTheListener`, but a Dispose that left the loopback listener open would pass. Only `does not throw` is tested.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/SocketActionGatedResolverTests.cs -Pattern 'Dispose_NeverSignalled_ClosesTheListenerAndLaterSignalsDoNothing' -Context 0,8
```

- Expected: The test asserts the listener is closed after Dispose.
- Actual: Lines 109-116 call Open, Dispose and Signal and assert nothing.

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
