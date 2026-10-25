---
id: AF-0430
title: CreateDispatcher_WildcardMatchAndInterleaveSet_BuildsTheDispatcher checks only that a dispatcher is not null
auditor: quality
severity: Low
status: accepted
reason: Stewart accepted it on 2026-10-25.
key: quality:Curl.Engine.UnitTests/ManagedEasyTransferTests.Wildcard.cs:CreateDispatcher_WildcardMatchAndInterleaveSet_BuildsTheDispatcher:weak-assertion
reproduction: none
task: BL-3496
tasks: BL-3496
found: 2026-10-24
found-at: 456765a13655caedad49326c988f9e4eec86da8b
scorecard: 2026-10-24_2106.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0430 - CreateDispatcher_WildcardMatchAndInterleaveSet_BuildsTheDispatcher checks only that a dispatcher is not null

## Summary

Low finding from the quality auditor at `Curl.Engine.UnitTests/ManagedEasyTransferTests.Wildcard.cs:156`: CreateDispatcher_WildcardMatchAndInterleaveSet_BuildsTheDispatcher checks only that a dispatcher is not null.

## Evidence

Location: `Curl.Engine.UnitTests/ManagedEasyTransferTests.Wildcard.cs:156`

The whole body after setup is Assert.IsNotNull(ManagedCurlEngine.CreateDispatcher(options, shared, cookieStore: null));. CreateDispatcher does not return null, so the assertion cannot fail; whether WildcardMatch and the Interleave callback reach the handlers is never checked. This is the same pattern AF-0417 fixed in CreateDispatcher_NoTimeout_BuildsTheProductionHandlers.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/ManagedEasyTransferTests.Wildcard.cs -Pattern 'WildcardMatchAndInterleaveSet_BuildsTheDispatcher' -Context 0,10
```

- Expected: An assertion on what the dispatcher does with WildcardMatch and Interleave set (e.g. which handler serves ftp).
- Actual: Line 156: Assert.IsNotNull(ManagedCurlEngine.CreateDispatcher(options, shared, cookieStore: null)); is the only assertion.

## Re-audits

- 2026-10-25 | 2026-10-25_0941.md | reproduces: no | Ran the Select-String reproduction: CreateDispatcher_WildcardMatchAndInterleaveSet_BuildsTheDispatcher (ManagedEasyTransferTests.Wildcard.cs:147) now asserts Assert.IsTrue(dispatcher.Serves(scheme)) for ftp, ftps and rtsp, and Assert.IsFalse(dispatcher.Serves("bogus")), not just that the dispatcher is not null.

## Log

- 2026-10-24: filed proposed.
- 2026-10-25: proposed -> accepted.
