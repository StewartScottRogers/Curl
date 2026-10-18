---
id: AF-0230
title: FromReport_NoReport_KeepsDefaultsAndTakesTheBytesTransferred asserts nothing; its checks only log
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Output.UnitTests/WriteOutTransferInfoTests.cs:FromReport_NoReport_KeepsDefaultsAndTakesTheBytesTransferred:no-assertion
reproduction: none
task: none
tasks:
found: 2026-10-16
found-at: 16500922292d0d0bcaede88acdabc2500cf08ebf
scorecard: 2026-10-16_1623.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0230 - FromReport_NoReport_KeepsDefaultsAndTakesTheBytesTransferred asserts nothing; its checks only log

## Summary

Medium finding from the quality auditor at `Curl.Output.UnitTests/WriteOutTransferInfoTests.cs:50`: FromReport_NoReport_KeepsDefaultsAndTakesTheBytesTransferred asserts nothing; its checks only log. Reported by an auditor flagged unreliable in 2026-10-16_1623.md.

## Evidence

Location: `Curl.Output.UnitTests/WriteOutTransferInfoTests.cs:50`

Lines 57-60 are diagnostics.Assert("effective url", RequestUrl, ...), ("download size", 12L, ...), ("total time", ...), ("primary port", 0L, ...). TestDiagnostics.Assert only logs (TestDiagnostics.cs:189-193), so the defaults and the bytes transferred that the name promises are never checked.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Output.UnitTests/WriteOutTransferInfoTests.cs -Pattern 'Assert\.' | Select-Object LineNumber
```

- Expected: Assert.* lines between 57 and 60.
- Actual: Only lines 70, 71 and 72.

## Re-audits

- 2026-10-18 | 2026-10-18_1045.md | reproduces: no | Ran the reproduction: Assert lines 69-72 inside FromReport_NoReport_KeepsDefaultsAndTakesTheBytesTransferred (AreEqual on EffectiveUrl, SizeDownload 12, TotalTime zero, PrimaryPort 0).

## Log

- 2026-10-16: filed proposed.
