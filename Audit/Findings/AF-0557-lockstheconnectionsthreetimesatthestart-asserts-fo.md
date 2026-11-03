---
id: AF-0557
title: LocksTheConnectionsThreeTimesAtTheStart asserts four locks over the whole perform
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Engine.UnitTests/ManagedShareHandleTests.cs:PerformAsync_ConnectionsSharedAndNoUrl_LocksTheConnectionsThreeTimesAtTheStart:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0557 - LocksTheConnectionsThreeTimesAtTheStart asserts four locks over the whole perform

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitTests/ManagedShareHandleTests.cs:122`: LocksTheConnectionsThreeTimesAtTheStart asserts four locks over the whole perform. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Engine.UnitTests/ManagedShareHandleTests.cs:122`

The name promises three Connect locks at the start; the test counts every Connect lock of the whole perform and asserts 'Assert.AreEqual(4, locks);'. Name and assertion disagree, so a change to the start-of-perform lock count is indistinguishable from one at the end.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/ManagedShareHandleTests.cs -Pattern 'LocksTheConnectionsThreeTimesAtTheStart|Assert.AreEqual\(4, locks\)'
```

- Expected: A test that counts the start's locks and asserts 3 (or a name that says four).
- Actual: Name says ThreeTimesAtTheStart; ManagedShareHandleTests.cs:122: Assert.AreEqual(4, locks);

## Re-audits

## Log

- 2026-11-03: filed proposed.
