---
id: AF-0292
title: Lib1502_OnARefusedAdd_RemovesAndCleansUp checks only the exit code, neither the removal nor the clean-up
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-22_0130.md: the reproduction no longer reproduces.
key: quality:Curl.Conformance.LibTest.UnitTests/Lib1485To1511Tests.cs:Lib1502_OnARefusedAdd_RemovesAndCleansUp:name-lies
reproduction: none
task: BL-2930
tasks: BL-2930
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed: 2026-10-22
closed-how: reliable-reaudit
closed-by: 2026-10-22_0130.md
---
# AF-0292 - Lib1502_OnARefusedAdd_RemovesAndCleansUp checks only the exit code, neither the removal nor the clean-up

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib1485To1511Tests.cs:292`: Lib1502_OnARefusedAdd_RemovesAndCleansUp checks only the exit code, neither the removal nor the clean-up.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib1485To1511Tests.cs:292`

Step 1 sample. With engine.Multi.AddResult = BadEasyHandle, the only assertion is Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);. FakeCurlEngine exposes DisposedEasyCount and FakeMultiTransfer exposes Removals, and Lib1502.cs:70-79 does DisposeAsync(handle) and multi?.RemoveHandle(easy). The test checks neither, so a port that skipped the clean-up it names would pass. This is the same pattern as AF-0283, in a different test.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1485To1511Tests.cs -Pattern 'Lib1502_OnARefusedAdd_RemovesAndCleansUp' -Context 0,9
```

- Expected: The body asserts engine.DisposedEasyCount and engine.Multi.Removals as well as the exit code.
- Actual: The body's only assertion is Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode).

## Re-audits

- 2026-10-22 | 2026-10-22_0130.md | reproduces: no | Ran the Select-String reproduction (Lib1485To1511Tests.cs:292-301). Besides the exit code, the test now asserts Assert.AreEqual(1, engine.Multi.Removals, "the refused handle is removed from the multi handle") and Assert.AreEqual(2, engine.DisposedEasyCount, "the original and the duplicated easy handle are both cleaned up").

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
- 2026-10-22: accepted -> closed. Re-audit 2026-10-22_0130.md: the reproduction no longer reproduces.
