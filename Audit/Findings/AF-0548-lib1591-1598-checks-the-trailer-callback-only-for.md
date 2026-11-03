---
id: AF-0548
title: Lib1591/1598 checks the trailer callback only for null
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitTests/Lib1589To1648Tests.cs:Lib1591And1598_SendTheTrailerHeaderAndCallbackAndAnswerTheirCode:weak-assertion
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
# AF-0548 - Lib1591/1598 checks the trailer callback only for null

## Summary

Low finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib1589To1648Tests.cs:124`: Lib1591/1598 checks the trailer callback only for null. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib1589To1648Tests.cs:124`

'Assert.IsNotNull(trailer);' is the only check of the callback the name says is sent; a callback that adds no trailers, or the wrong ones, passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1589To1648Tests.cs -Pattern 'Assert.IsNotNull\(trailer\)'
```

- Expected: The callback is invoked and its trailer lines are asserted.
- Actual: Lib1589To1648Tests.cs:124: Assert.IsNotNull(trailer);

## Re-audits

## Log

- 2026-11-03: filed proposed.
