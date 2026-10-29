---
id: AF-0459
title: Of_NeitherCallbackSet_GivesTheBuiltInMeterAlone asserts only that CallbackTransferProgress.Of returned something
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Engine.UnitTests/CallbackTransferProgressTests.cs:Of_NeitherCallbackSet_GivesTheBuiltInMeterAlone:weak-assertion
reproduction: none
task: BL-4240
tasks: BL-4240
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0459 - Of_NeitherCallbackSet_GivesTheBuiltInMeterAlone asserts only that CallbackTransferProgress.Of returned something

## Summary

Low finding from the quality auditor at `Curl.Engine.UnitTests/CallbackTransferProgressTests.cs:46`: Of_NeitherCallbackSet_GivesTheBuiltInMeterAlone asserts only that CallbackTransferProgress.Of returned something. Reported by an auditor flagged unreliable in 2026-10-29_0606.md.

## Evidence

Location: `Curl.Engine.UnitTests/CallbackTransferProgressTests.cs:46`

Found by Find-WeakTests.ps1 (weak-assertion, only Assert.IsNotNull). The whole body is `Assert.IsNotNull(CallbackTransferProgress.Of(noProgress: false, new EasyCallbackOptions(), abort, () => Stream.Null, TimeProvider.System));`. CallbackTransferProgress.Of (Curl.Engine.UnitLibrary/CallbackTransferProgress.cs:54-68) returns a callback-calling progress when TransferInfo or Progress is set, and the built-in meter otherwise. The test would also pass if the no-callback branch returned a callback-wrapping progress, so `the built-in meter alone` is never checked.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/CallbackTransferProgressTests.cs -Pattern 'Of_NeitherCallbackSet_GivesTheBuiltInMeterAlone' -Context 0,5
```

- Expected: The test asserts the result is the built-in meter with no callback (for example, that reporting progress calls nothing and draws the meter).
- Actual: Its only assertion is Assert.IsNotNull(CallbackTransferProgress.Of(...)).

## Re-audits

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
